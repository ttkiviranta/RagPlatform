using System.Diagnostics;
using Microsoft.Extensions.Options;
using RagPlatform.Api.Contracts.Audit;
using RagPlatform.Api.Contracts.Embeddings;
using RagPlatform.Api.Contracts.Identity;
using RagPlatform.Api.Options;
using RagPlatform.Api.Services;

namespace RagPlatform.Api.Providers;

public sealed class FoundryEmbeddingProvider : IEmbeddingProvider
{
    private readonly FoundryInferenceClient _inferenceClient;
    private readonly FoundryOptions _options;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditEventWriter _auditEventWriter;

    public FoundryEmbeddingProvider(
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

    public string ProviderName => "Azure AI Foundry";

    public string ModelName => RequireDeployment();

    public async Task<IReadOnlyList<float>> GenerateEmbeddingAsync(
        string content,
        string? modelName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        var deployment = RequireDeployment();
        if (!string.IsNullOrWhiteSpace(modelName)
            && !string.Equals(modelName, deployment, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"The configured embedding deployment is '{deployment}'.",
                nameof(modelName));
        }

        var currentUser = await _currentUserService.GetCurrentUserAsync(cancellationToken);
        var stopwatch = Stopwatch.StartNew();
        (IReadOnlyList<float> Vector, int? InputTokenCount) result;
        try
        {
            result = await _inferenceClient.GenerateEmbeddingAsync(
                deployment,
                content,
                cancellationToken);
            if (result.Vector.Count != _options.EmbeddingDimensions)
            {
                throw new InvalidOperationException(
                    $"The configured embedding dimension is {_options.EmbeddingDimensions}, but the deployment returned {result.Vector.Count}.");
            }
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
                    "Embedding",
                    deployment,
                    durationMilliseconds: stopwatch.Elapsed.TotalMilliseconds,
                    inputCharacterCount: content.Length,
                    modelProvider: ProviderName,
                    modelDeployment: deployment,
                    modelOperation: "Embedding");
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
                "Embedding",
                deployment,
                durationMilliseconds: stopwatch.Elapsed.TotalMilliseconds,
                inputCharacterCount: content.Length,
                modelProvider: ProviderName,
                modelDeployment: deployment,
                modelOperation: "Embedding",
                inputTokenCount: result.InputTokenCount);
            await _auditEventWriter.FlushAsync(cancellationToken);
        }

        return result.Vector;
    }

    private string RequireDeployment()
    {
        if (string.IsNullOrWhiteSpace(_options.EmbeddingDeployment))
        {
            throw new InvalidOperationException("Foundry:EmbeddingDeployment must be configured.");
        }

        if (_options.EmbeddingDimensions <= 0)
        {
            throw new InvalidOperationException("Foundry:EmbeddingDimensions must be greater than zero.");
        }

        return _options.EmbeddingDeployment.Trim();
    }
}
