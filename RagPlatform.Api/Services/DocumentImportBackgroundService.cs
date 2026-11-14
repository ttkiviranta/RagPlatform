using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;
using RagPlatform.Api.Contracts.Documents;
using RagPlatform.Api.Contracts.Processing;
using RagPlatform.Api.Models;
using RagPlatform.Api.Options;

namespace RagPlatform.Api.Services;

public sealed class DocumentImportBackgroundService : BackgroundService
{
    private readonly ServiceBusClient _serviceBusClient;
    private readonly ServiceBusOptions _serviceBusOptions;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<DocumentImportBackgroundService> _logger;

    public DocumentImportBackgroundService(
        ServiceBusClient serviceBusClient,
        IOptions<ServiceBusOptions> serviceBusOptions,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<DocumentImportBackgroundService> logger)
    {
        _serviceBusClient = serviceBusClient;
        _serviceBusOptions = serviceBusOptions.Value;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var processor = _serviceBusClient.CreateProcessor(
            _serviceBusOptions.ImportQueueName,
            new ServiceBusProcessorOptions
            {
                AutoCompleteMessages = false,
                MaxConcurrentCalls = 1,
                PrefetchCount = 0
            });

        processor.ProcessMessageAsync += ProcessMessageAsync;
        processor.ProcessErrorAsync += ProcessErrorAsync;

        var processingStarted = false;
        try
        {
            await processor.StartProcessingAsync(stoppingToken);
            processingStarted = true;
            _logger.LogInformation(
                "Started document import message processor for queue {QueueName}.",
                _serviceBusOptions.ImportQueueName);

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            if (processingStarted)
            {
                await processor.StopProcessingAsync(CancellationToken.None);
            }

            _logger.LogInformation(
                "Stopped document import message processor for queue {QueueName}.",
                _serviceBusOptions.ImportQueueName);
        }
    }

    private async Task ProcessMessageAsync(ProcessMessageEventArgs args)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var messageHandler = scope.ServiceProvider.GetRequiredService<DocumentImportMessageHandler>();
        var importProcessor = scope.ServiceProvider.GetRequiredService<DocumentImportProcessor>();
        var settlementPolicy = scope.ServiceProvider.GetRequiredService<IDocumentImportSettlementPolicy>();
        Document? document = null;

        try
        {
            document = await messageHandler.HandleAsync(args.Message.Body.ToMemory(), args.CancellationToken);
            await importProcessor.ProcessAsync(document, args.CancellationToken);
            await importProcessor.ValidateCompletedImportAsync(document.Id, args.CancellationToken);
        }
        catch (OperationCanceledException) when (args.CancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (InvalidDocumentImportMessageException exception)
        {
            await args.DeadLetterMessageAsync(
                args.Message,
                deadLetterReason: "InvalidDocumentImportMessage",
                deadLetterErrorDescription: "The document import envelope is invalid and cannot be processed.",
                cancellationToken: args.CancellationToken);
            _logger.LogWarning(
                "Dead-lettered import message {MessageId} due to invalid envelope kind {FailureKind}.",
                args.Message.MessageId,
                exception.FailureKind);
            return;
        }
        catch (Exception exception)
        {
            if (document is null)
            {
                throw;
            }

            var disposition = await settlementPolicy.DecideAsync(document, exception, args.CancellationToken);
            if (disposition == DocumentImportSettlementDisposition.DeadLetter)
            {
                var errorDescription = exception is PermanentPdfFailureException permanentFailure
                    ? permanentFailure.Message
                    : "A permanent PDF failure was already persisted for this document.";
                await args.DeadLetterMessageAsync(
                    args.Message,
                    deadLetterReason: "PermanentPdfFailure",
                    deadLetterErrorDescription: errorDescription,
                    cancellationToken: args.CancellationToken);
                _logger.LogWarning(
                    "Dead-lettered import message {MessageId} after the permanent PDF failure state and audit were persisted for document {DocumentId}.",
                    args.Message.MessageId,
                    document.Id);
                return;
            }

            if (disposition != DocumentImportSettlementDisposition.Retry)
            {
                throw new InvalidOperationException(
                    $"Settlement policy returned unexpected disposition {disposition} for a failed import.",
                    exception);
            }

            _logger.LogWarning(
                exception,
                "Leaving import message {MessageId} unsettled for retry after processing failed for document {DocumentId}.",
                args.Message.MessageId,
                document.Id);
            return;
        }

        var processedDocument = document
            ?? throw new InvalidOperationException("The import message was not validated before settlement.");
        var successDisposition = await settlementPolicy.DecideAsync(processedDocument, null, args.CancellationToken);
        if (successDisposition == DocumentImportSettlementDisposition.Complete)
        {
            await args.CompleteMessageAsync(args.Message, args.CancellationToken);
            _logger.LogInformation(
                "Completed import message {MessageId} after document {DocumentId} completion evidence was validated.",
                args.Message.MessageId,
                processedDocument.Id);
            return;
        }

        if (successDisposition != DocumentImportSettlementDisposition.Defer)
        {
            throw new InvalidOperationException(
                $"Settlement policy returned unexpected disposition {successDisposition} after successful processing.");
        }

        _logger.LogInformation(
            "Leaving import message {MessageId} unsettled because document {DocumentId} is not ready for completion.",
            args.Message.MessageId,
            processedDocument.Id);
    }

    private Task ProcessErrorAsync(ProcessErrorEventArgs args)
    {
        _logger.LogError(
            args.Exception,
            "Service Bus document import processor failed. ErrorSource={ErrorSource}, EntityPath={EntityPath}",
            args.ErrorSource,
            args.EntityPath);

        return Task.CompletedTask;
    }
}
