using Microsoft.EntityFrameworkCore;
using RagPlatform.Api.Contracts.Conversations;
using RagPlatform.Api.Contracts.Identity;
using RagPlatform.Api.Data;
using RagPlatform.Api.Models;

namespace RagPlatform.Api.Services;

public sealed class ConversationMessageService : IConversationMessageService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public ConversationMessageService(
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
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
