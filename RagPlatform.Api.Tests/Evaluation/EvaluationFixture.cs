using Microsoft.Extensions.Logging.Abstractions;
using RagPlatform.Api.Contracts.Rag;
using RagPlatform.Api.Options;
using RagPlatform.Api.Services;
using RagPlatform.Api.Tests.Services;

namespace RagPlatform.Api.Tests.Evaluation;

internal static class EvaluationFixture
{
    public static EvaluationCase GetCase(string caseId) =>
        EvaluationCases.All.Single(testCase => testCase.CaseId == caseId);

    public static FoundryAnswerGenerationService CreateAnswerService(
        RagQualityHardeningTests.CapturingChatCompletionService chat) =>
        new(
            new PromptBuilder(),
            chat,
            new NoOpTelemetryService(),
            Microsoft.Extensions.Options.Options.Create(new FoundryOptions { ChatDeployment = "test-chat-deployment" }),
            NullLogger<FoundryAnswerGenerationService>.Instance);

    public static RagCitationDto CreateCitation(string content) => new()
    {
        CitationNumber = 1,
        DocumentId = Guid.Parse("20000000-0000-0000-0000-000000000001"),
        ChunkId = Guid.Parse("30000000-0000-0000-0000-000000000001"),
        SourceName = "evaluation.txt",
        Excerpt = content
    };
}
