using Microsoft.EntityFrameworkCore;
using RagPlatform.Api.Contracts.Audit;
using RagPlatform.Api.Contracts.Conversations;
using RagPlatform.Api.Contracts.Identity;
using RagPlatform.Api.Data;
using RagPlatform.Api.Models;

namespace RagPlatform.Api.Services;

public sealed class ConversationService : IConversationService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditEventWriter _auditEventWriter;

    public ConversationService(
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IAuditEventWriter auditEventWriter)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _auditEventWriter = auditEventWriter;
    }

    public async Task<IReadOnlyCollection<ConversationResponse>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var currentUser = await GetRequiredCurrentUserAsync(cancellationToken);

        return await _dbContext.Conversations
            .AsNoTracking()
            .Where(conversation => conversation.UserId == currentUser.Id)
            .OrderByDescending(conversation => conversation.UpdatedAtUtc)
            .Select(conversation => new ConversationResponse
            {
                Id = conversation.Id,
                Title = conversation.Title,
                CreatedAtUtc = conversation.CreatedAtUtc,
                UpdatedAtUtc = conversation.UpdatedAtUtc
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<ConversationResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var currentUser = await GetRequiredCurrentUserAsync(cancellationToken);

        var conversation = await _dbContext.Conversations
            .AsNoTracking()
            .Where(conversation => conversation.Id == id && conversation.UserId == currentUser.Id)
            .Select(conversation => new ConversationResponse
            {
                Id = conversation.Id,
                Title = conversation.Title,
                CreatedAtUtc = conversation.CreatedAtUtc,
                UpdatedAtUtc = conversation.UpdatedAtUtc
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (conversation is null)
        {
            if (await _dbContext.Conversations.AnyAsync(item => item.Id == id, cancellationToken))
            {
                await RecordDeniedAccessAsync(currentUser, id, cancellationToken);
            }

            return null;
        }

        return conversation;
    }

    public async Task<ConversationResponse> CreateAsync(
        CreateConversationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new ArgumentException("A conversation title is required.", nameof(request.Title));
        }

        var currentUser = await GetRequiredCurrentUserAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var conversation = new Conversation
        {
            Title = request.Title.Trim(),
            UserId = currentUser.Id,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _dbContext.Conversations.Add(conversation);
        _auditEventWriter.Add(
            currentUser.Id,
            currentUser.TenantId,
            AuditEventTypes.ConversationCreated,
            "Succeeded",
            "Conversation",
            conversation.Id.ToString());
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(conversation);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var currentUser = await GetRequiredCurrentUserAsync(cancellationToken);
        var conversation = await _dbContext.Conversations
            .FirstOrDefaultAsync(
                item => item.Id == id && item.UserId == currentUser.Id,
                cancellationToken);

        if (conversation is null)
        {
            if (await _dbContext.Conversations.AnyAsync(item => item.Id == id, cancellationToken))
            {
                await RecordDeniedAccessAsync(currentUser, id, cancellationToken);
            }

            return false;
        }

        _auditEventWriter.Add(
            currentUser.Id,
            currentUser.TenantId,
            AuditEventTypes.ConversationDeleted,
            "Succeeded",
            "Conversation",
            id.ToString());
        _dbContext.Conversations.Remove(conversation);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task RecordDeniedAccessAsync(User currentUser, Guid conversationId, CancellationToken cancellationToken)
    {
        _auditEventWriter.Add(
            currentUser.Id,
            currentUser.TenantId,
            AuditEventTypes.ConversationAccessDenied,
            "Denied",
            "Conversation",
            conversationId.ToString());
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<User> GetRequiredCurrentUserAsync(CancellationToken cancellationToken)
    {
        var currentUser = await _currentUserService.GetCurrentUserAsync(cancellationToken);
        if (currentUser is not { IsActive: true })
        {
            throw new UnauthorizedAccessException("An active application user is required.");
        }

        return currentUser;
    }

    private static ConversationResponse ToResponse(Conversation conversation)
    {
        return new ConversationResponse
        {
            Id = conversation.Id,
            Title = conversation.Title,
            CreatedAtUtc = conversation.CreatedAtUtc,
            UpdatedAtUtc = conversation.UpdatedAtUtc
        };
    }
}
