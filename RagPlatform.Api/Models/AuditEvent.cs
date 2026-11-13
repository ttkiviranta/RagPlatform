namespace RagPlatform.Api.Models;

public sealed class AuditEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string TenantId { get; set; } = string.Empty;

    public Guid? ActorUserId { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string Outcome { get; set; } = string.Empty;

    public string? TargetType { get; set; }

    public string? TargetId { get; set; }

    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

    public string? CorrelationId { get; set; }

    public string? HttpMethod { get; set; }

    public string? RequestPath { get; set; }

    public double? DurationMilliseconds { get; set; }

    public int? InputCharacterCount { get; set; }

    public int? RequestedTopK { get; set; }

    public int? ResultCount { get; set; }

    public double? AverageSimilarity { get; set; }

    public int? ContextCharacterCount { get; set; }

    public int? PromptCharacterCount { get; set; }

    public string? ModelProvider { get; set; }

    public string? ModelDeployment { get; set; }

    public string? ModelOperation { get; set; }

    public int? InputTokenCount { get; set; }

    public int? OutputTokenCount { get; set; }

    public int? GeneratedEmbeddingCount { get; set; }

    public int? ReusedEmbeddingCount { get; set; }
}
