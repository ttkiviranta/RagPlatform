using Microsoft.EntityFrameworkCore;
using RagPlatform.Api.Contracts.Audit;
using RagPlatform.Api.Contracts.Conversations;
using RagPlatform.Api.Contracts.Identity;
using RagPlatform.Api.Data;
using RagPlatform.Api.Models;

namespace RagPlatform.Api.Services;

public sealed class ConversationMessageService : IConversationMessageService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditEventWriter _auditEventWriter;

    public ConversationMessageService(
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IAuditEventWriter auditEventWriter)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _auditEventWriter = auditEventWriter;
    }

    public async Task<IReadOnlyCollection<ConversationMessageResponse>?> GetMessagesAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        var currentUser = await GetRequiredCurrentUserAsync(cancellationToken);
        var ownsConversation = await _dbContext.Conversations
            .AnyAsync(
                conversation => conversation.Id == conversationId && conversation.UserId == currentUser.Id,
                cancellationToken);
        if (!ownsConversation)
        {
            if (await _dbContext.Conversations.AnyAsync(conversation => conversation.Id == conversationId, cancellationToken))
            {
                await RecordDeniedAccessAsync(currentUser, conversationId, cancellationToken);
            }

            return null;
        }

        return await _dbContext.ConversationMessages
            .AsNoTracking()
            .Where(message =>
                message.ConversationId == conversationId
                && message.Conversation!.UserId == currentUser.Id)
            .OrderBy(message => message.CreatedAtUtc)
            .ThenBy(message => message.Id)
            .Select(message => new ConversationMessageResponse
            {
                Id = message.Id,
                ConversationId = message.ConversationId,
                Role = message.Role.ToString(),
                Content = message.Content,
                CreatedAtUtc = message.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<ConversationMessageResponse?> CreateUserMessageAsync(
        Guid conversationId,
        CreateConversationMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            throw new ArgumentException("Message content is required.", nameof(request.Content));
        }

        var currentUser = await GetRequiredCurrentUserAsync(cancellationToken);
        var conversation = await _dbContext.Conversations
            .FirstOrDefaultAsync(
                item => item.Id == conversationId && item.UserId == currentUser.Id,
                cancellationToken);

        if (conversation is null)
        {
            if (await _dbContext.Conversations.AnyAsync(item => item.Id == conversationId, cancellationToken))
            {
                await RecordDeniedAccessAsync(currentUser, conversationId, cancellationToken);
            }

            return null;
        }

        var message = new ConversationMessage
        {
            ConversationId = conversation.Id,
            Role = ConversationMessageRole.User,
            Content = request.Content,
            CreatedAtUtc = DateTime.UtcNow
        };

        conversation.UpdatedAtUtc = message.CreatedAtUtc;
        _dbContext.ConversationMessages.Add(message);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ConversationMessageResponse
        {
            Id = message.Id,
            ConversationId = message.ConversationId,
            Role = message.Role.ToString(),
            Content = message.Content,
            CreatedAtUtc = message.CreatedAtUtc
        };
    }

    public async Task<IReadOnlyList<ConversationMessageResponse>?> GetRecentMessagesAsync(
        Guid conversationId,
        int maxCount,
        CancellationToken cancellationToken = default)
    {
        if (maxCount <= 0 || maxCount > 50)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCount), "Message history must be between 1 and 50 messages.");
        }

        var currentUser = await GetRequiredCurrentUserAsync(cancellationToken);
        var ownsConversation = await _dbContext.Conversations
            .AnyAsync(
                conversation => conversation.Id == conversationId && conversation.UserId == currentUser.Id,
                cancellationToken);
        if (!ownsConversation)
        {
            if (await _dbContext.Conversations.AnyAsync(conversation => conversation.Id == conversationId, cancellationToken))
            {
                await RecordDeniedAccessAsync(currentUser, conversationId, cancellationToken);
            }

            return null;
        }

        var messages = await _dbContext.ConversationMessages
            .AsNoTracking()
            .Where(message =>
                message.ConversationId == conversationId
                && message.Conversation!.UserId == currentUser.Id)
            .OrderByDescending(message => message.CreatedAtUtc)
            .ThenByDescending(message => message.Id)
            .Take(maxCount)
            .ToListAsync(cancellationToken);

        return messages
            .OrderBy(message => message.CreatedAtUtc)
            .ThenBy(message => message.Id)
            .Select(MapMessage)
            .ToList();
    }

    public async Task<bool> AppendExchangeAsync(
        Guid conversationId,
        string userContent,
        string assistantContent,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userContent);
        ArgumentException.ThrowIfNullOrWhiteSpace(assistantContent);

        var currentUser = await GetRequiredCurrentUserAsync(cancellationToken);
        var conversation = await _dbContext.Conversations
            .FirstOrDefaultAsync(
                item => item.Id == conversationId && item.UserId == currentUser.Id,
                cancellationToken);
        if (conversation is null)
        {
            if (await _dbContext.Conversations.AnyAsync(item => item.Id == conversationId, cancellationToken))
            {
                await RecordDeniedAccessAsync(currentUser, conversationId, cancellationToken);
            }

            return false;
        }

        var now = DateTime.UtcNow;
        _dbContext.ConversationMessages.AddRange(
            new ConversationMessage
            {
                ConversationId = conversation.Id,
                Role = ConversationMessageRole.User,
                Content = userContent,
                CreatedAtUtc = now
            },
            new ConversationMessage
            {
                ConversationId = conversation.Id,
                Role = ConversationMessageRole.Assistant,
                Content = assistantContent,
                CreatedAtUtc = now.AddMilliseconds(1)
            });
        conversation.UpdatedAtUtc = now.AddMilliseconds(1);
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

    private static ConversationMessageResponse MapMessage(ConversationMessage message)
    {
        return new ConversationMessageResponse
        {
            Id = message.Id,
            ConversationId = message.ConversationId,
            Role = message.Role.ToString(),
            Content = message.Content,
            CreatedAtUtc = message.CreatedAtUtc
        };
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
}
