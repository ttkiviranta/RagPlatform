using System.Diagnostics;
using RagPlatform.Api.Contracts.Audit;
using RagPlatform.Api.Data;
using RagPlatform.Api.Models;

namespace RagPlatform.Api.Services;

public sealed class AuditEventWriter : IAuditEventWriter
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditEventWriter(
        ApplicationDbContext dbContext,
        IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
    }

    public void Add(
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
        int? promptCharacterCount = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(outcome);

        var httpContext = _httpContextAccessor.HttpContext;
        _dbContext.AuditEvents.Add(new AuditEvent
        {
            TenantId = tenantId,
            ActorUserId = actorUserId,
            EventType = eventType,
            Outcome = outcome,
            TargetType = targetType,
            TargetId = targetId,
            OccurredAtUtc = DateTime.UtcNow,
            CorrelationId = Activity.Current?.TraceId.ToString() ?? httpContext?.TraceIdentifier,
            HttpMethod = httpContext?.Request.Method,
            RequestPath = httpContext?.Request.Path.Value,
            DurationMilliseconds = durationMilliseconds,
            InputCharacterCount = inputCharacterCount,
            RequestedTopK = requestedTopK,
            ResultCount = resultCount,
            AverageSimilarity = averageSimilarity,
            ContextCharacterCount = contextCharacterCount,
            PromptCharacterCount = promptCharacterCount
        });
    }

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
