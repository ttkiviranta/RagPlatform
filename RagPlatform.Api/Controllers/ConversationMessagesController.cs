using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RagPlatform.Api.Authorization;
using RagPlatform.Api.Contracts.Conversations;

namespace RagPlatform.Api.Controllers;

[Authorize(Policy = AuthorizationPolicies.ActiveUser)]
[ApiController]
[Route("api/conversations/{conversationId:guid}/messages")]
public sealed class ConversationMessagesController : ControllerBase
{
    private readonly IConversationMessageService _conversationMessageService;

    public ConversationMessagesController(IConversationMessageService conversationMessageService)
    {
        _conversationMessageService = conversationMessageService;
    }

    [HttpGet(Name = nameof(GetMessagesAsync))]
    [ProducesResponseType(typeof(IReadOnlyCollection<ConversationMessageResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyCollection<ConversationMessageResponse>>> GetMessagesAsync(
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        var messages = await _conversationMessageService.GetMessagesAsync(conversationId, cancellationToken);
        if (messages is null)
        {
            return NotFound();
        }

        return Ok(messages);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ConversationMessageResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConversationMessageResponse>> CreateAsync(
        Guid conversationId,
        [FromBody] CreateConversationMessageRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var message = await _conversationMessageService.CreateUserMessageAsync(
                conversationId,
                request,
                cancellationToken);

            if (message is null)
            {
                return NotFound();
            }

            return CreatedAtRoute(nameof(GetMessagesAsync), new { conversationId }, message);
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(exception.ParamName ?? string.Empty, exception.Message);
            return ValidationProblem(ModelState);
        }
    }
}
