using System.Diagnostics;
using Microsoft.Extensions.Options;
using RagPlatform.Api.Contracts.Audit;
using RagPlatform.Api.Contracts.Identity;
using RagPlatform.Api.Contracts.Rag;
using RagPlatform.Api.Options;

namespace RagPlatform.Api.Services;

public sealed class FoundryChatCompletionService : IFoundryChatCompletionService
{
    private readonly FoundryInferenceClient _inferenceClient;
    private readonly FoundryOptions _options;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditEventWriter _auditEventWriter;

    public FoundryChatCompletionService(
        FoundryInferenceClient inferenceClient,
        IOptions<FoundryOptions> options,
        ICurrentUserService currentUserService,
        IAuditEventWriter auditEventWriter)
    {
        _inferenceClient = inferenceClient;
        _options = options.Value;
        _currentUserService = currentUserService;
        _auditEventWriter = auditEventWriter;
    }

    public async Task<FoundryChatCompletionResponse> CompleteAsync(
        string systemPrompt,
        IReadOnlyList<RagChatMessage> messages,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(systemPrompt);
        ArgumentNullException.ThrowIfNull(messages);
        if (string.IsNullOrWhiteSpace(_options.ChatDeployment))
        {
            throw new InvalidOperationException("Foundry:ChatDeployment must be configured.");
        }

        if (_options.MaxOutputTokens <= 0)
        {
            throw new InvalidOperationException("Foundry:MaxOutputTokens must be greater than zero.");
        }

        var deployment = _options.ChatDeployment.Trim();
        var currentUser = await _currentUserService.GetCurrentUserAsync(cancellationToken);
        var stopwatch = Stopwatch.StartNew();
        var chatMessages = new List<RagChatMessage>
        {
            new() { Role = "System", Content = systemPrompt }
        };
        foreach (var message in messages)
        {
            if (string.IsNullOrWhiteSpace(message.Content))
            {
                continue;
            }

            if (string.Equals(message.Role, "User", StringComparison.OrdinalIgnoreCase))
            {
                chatMessages.Add(new RagChatMessage { Role = "User", Content = message.Content });
            }
            else if (string.Equals(message.Role, "Assistant", StringComparison.OrdinalIgnoreCase))
            {
                chatMessages.Add(new RagChatMessage { Role = "Assistant", Content = message.Content });
            }
        }

        (string Answer, int? InputTokenCount, int? OutputTokenCount) completion;
        try
        {
            completion = await _inferenceClient.CompleteChatAsync(
                deployment,
                chatMessages,
                _options.MaxOutputTokens,
                cancellationToken);
        }
        catch
        {
            if (currentUser is { IsActive: true })
            {
                _auditEventWriter.Add(
                    currentUser.Id,
                    currentUser.TenantId,
                    AuditEventTypes.FoundryModelInvocation,
                    "Failed",
                    "ChatCompletion",
                    deployment,
                    durationMilliseconds: stopwatch.Elapsed.TotalMilliseconds,
                    modelProvider: "Azure AI Foundry",
                    modelDeployment: deployment,
                    modelOperation: "ChatCompletion");
                await _auditEventWriter.FlushAsync(cancellationToken);
            }

            throw;
        }

        if (currentUser is { IsActive: true })
        {
            _auditEventWriter.Add(
                currentUser.Id,
                currentUser.TenantId,
                AuditEventTypes.FoundryModelInvocation,
                "Succeeded",
                "ChatCompletion",
                deployment,
                durationMilliseconds: stopwatch.Elapsed.TotalMilliseconds,
                inputCharacterCount: systemPrompt.Length + messages.Sum(message => message.Content.Length),
                modelProvider: "Azure AI Foundry",
                modelDeployment: deployment,
                modelOperation: "ChatCompletion",
                inputTokenCount: completion.InputTokenCount,
                outputTokenCount: completion.OutputTokenCount);
            await _auditEventWriter.FlushAsync(cancellationToken);
        }

        return new FoundryChatCompletionResponse
        {
            Answer = completion.Answer,
            InputTokenCount = completion.InputTokenCount,
            OutputTokenCount = completion.OutputTokenCount
        };
    }
}
