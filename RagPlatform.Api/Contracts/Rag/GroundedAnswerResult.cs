namespace RagPlatform.Api.Contracts.Rag;

public sealed record GroundedAnswerResult(
    string Answer,
    IReadOnlyList<int> CitationNumbers,
    int ContextCharacterCount,
    int PromptCharacterCount,
    string? AnswerSource = null);