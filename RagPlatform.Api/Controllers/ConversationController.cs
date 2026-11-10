using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RagPlatform.Api.Authorization;
using RagPlatform.Api.Contracts.Conversations;

namespace RagPlatform.Api.Controllers;

[Authorize(Policy = AuthorizationPolicies.ActiveUser)]
[ApiController]
[Route("api/conversations")]
public sealed class ConversationController : ControllerBase
{
    private readonly IConversationService _conversationService;

    public ConversationController(IConversationService conversationService)
    {
        _conversationService = conversationService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<ConversationResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<ConversationResponse>>> GetAllAsync(
        CancellationToken cancellationToken)
    {
        var conversations = await _conversationService.GetAllAsync(cancellationToken);
        return Ok(conversations);
    }

    [HttpGet("{id:guid}", Name = nameof(GetByIdAsync))]
    [ProducesResponseType(typeof(ConversationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConversationResponse>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var conversation = await _conversationService.GetByIdAsync(id, cancellationToken);
        if (conversation is null)
        {
            return NotFound();
        }

        return Ok(conversation);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ConversationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ConversationResponse>> CreateAsync(
        [FromBody] CreateConversationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var conversation = await _conversationService.CreateAsync(request, cancellationToken);
            return CreatedAtRoute(nameof(GetByIdAsync), new { id = conversation.Id }, conversation);
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(exception.ParamName ?? string.Empty, exception.Message);
            return ValidationProblem(ModelState);
        }
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await _conversationService.DeleteAsync(id, cancellationToken);
        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }
}
