namespace RagPlatform.Api.Contracts.Audit;

public interface IAuditEventWriter
{
    void Add(
        Guid? actorUserId,
        string tenantId,
        string eventType,
        string outcome,
        string? targetType = null,
        string? targetId = null,
        double? durationMilliseconds = null,
        int? inputCharacterCount = null,
        int? requestedTopK = null,
        int? resultCount = null,
        double? averageSimilarity = null,
        int? contextCharacterCount = null,
        int? promptCharacterCount = null,
        string? modelProvider = null,
        string? modelDeployment = null,
        string? modelOperation = null,
        int? inputTokenCount = null,
        int? outputTokenCount = null,
        int? generatedEmbeddingCount = null,
        int? reusedEmbeddingCount = null);

    Task FlushAsync(CancellationToken cancellationToken = default);
}
