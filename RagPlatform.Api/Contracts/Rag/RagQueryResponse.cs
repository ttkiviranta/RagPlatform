using System.Text.Json.Serialization;

namespace RagPlatform.Api.Contracts.Rag;

public sealed class RagQueryResponse
{
    public Guid ConversationId { get; set; }

    public string Answer { get; set; } = string.Empty;

    public IReadOnlyList<RagCitationDto> Citations { get; set; } = Array.Empty<RagCitationDto>();

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AnswerSource { get; set; }
}
