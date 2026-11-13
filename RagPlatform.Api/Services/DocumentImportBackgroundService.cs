using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;
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
        var document = await messageHandler.HandleAsync(args.Message.Body.ToMemory(), args.CancellationToken);

        _logger.LogInformation(
            "Validated import message {MessageId} for document {DocumentId}; status remains {ImportStatus} and the message is left unsettled.",
            args.Message.MessageId,
            document.Id,
            document.ImportStatus);
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
