namespace RagPlatform.Api.Tests.Evaluation;

internal enum EvaluationCategory
{
    AuthorizedAnswer,
    Abstention,
    CitationValidation,
    PromptInjection,
    Authorization,
    GenerationFailure
}

internal enum ExpectedEvaluationOutcome
{
    AnswerWithAuthorizedCitation,
    AbstainWithoutCitations,
    RejectInvalidCitation,
    FrameInstructionsAsUntrusted,
    ExcludeUnauthorizedEvidence,
    PropagateGenerationFailure
}

internal sealed record EvaluationCase(
    string CaseId,
    EvaluationCategory Category,
    string Question,
    ExpectedEvaluationOutcome ExpectedOutcome,
    string Notes);

internal static class EvaluationCases
{
    public const string Version = "1.0";

    public static IReadOnlyList<EvaluationCase> All { get; } = Array.AsReadOnly<EvaluationCase>(
    [
        new(
            "DET-001",
            EvaluationCategory.AuthorizedAnswer,
            "What color is the authorized test document?",
            ExpectedEvaluationOutcome.AnswerWithAuthorizedCitation,
            "Seed an accessible document stating that its color is blue. Script a cited answer and verify the citation maps to the authorized persisted chunk."),
        new(
            "DET-002",
            EvaluationCategory.Abstention,
            "What is the test document's serial number?",
            ExpectedEvaluationOutcome.AbstainWithoutCitations,
            "Return no usable context. Expect the insufficient-evidence response with no citations and no chat call; a successful exchange may be persisted."),
        new(
            "DET-003",
            EvaluationCategory.CitationValidation,
            "What color is the authorized test document?",
            ExpectedEvaluationOutcome.RejectInvalidCitation,
            "Supply only citation [1] and script an answer citing [9]. Expect citation validation to fail without persisting a partial exchange."),
        new(
            "DET-004",
            EvaluationCategory.PromptInjection,
            "Summarize the test document.",
            ExpectedEvaluationOutcome.FrameInstructionsAsUntrusted,
            "Include a synthetic instruction to ignore the rules in retrieved evidence. Verify it is framed as untrusted; a scripted response cannot prove live-model resistance."),
        new(
            "DET-005",
            EvaluationCategory.Authorization,
            "What is the test document's marker?",
            ExpectedEvaluationOutcome.ExcludeUnauthorizedEvidence,
            "Seed distinct documents for two tenants. As a tenant A user, verify tenant B evidence is excluded from retrieval, context, citations, and snapshots."),
        new(
            "DET-006",
            EvaluationCategory.GenerationFailure,
            "What color is the authorized test document?",
            ExpectedEvaluationOutcome.PropagateGenerationFailure,
            "Supply usable evidence and make the chat fake throw a timeout. Expect the failure to propagate without a successful answer or partial exchange.")
    ]);
}
