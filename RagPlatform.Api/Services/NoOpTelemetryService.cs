using RagPlatform.Api.Contracts.Telemetry;

namespace RagPlatform.Api.Services;

public sealed class NoOpTelemetryService : ITelemetryService
{
    public void TrackApiRequest(string route, int statusCode, double durationMs, string method)
    {
    }

    public void TrackDatabaseQuery(string operation, double durationMs, bool success)
    {
    }

    public void TrackDocumentProcessing(string operation, double durationMs, bool success, int itemCount = 0)
    {
    }

    public void TrackDocumentChunking(double durationMs, bool success, int generatedChunkCount, int reusedChunkCount)
    {
    }

    public void TrackEmbeddingGeneration(string modelName, double durationMs, int vectorDimensions, bool success)
    {
    }

    public void TrackEmbeddingBatch(
        string modelName,
        double durationMs,
        bool success,
        int generatedEmbeddingCount,
        int reusedEmbeddingCount)
    {
    }

    public void TrackVectorSearch(
        string modelName,
        int topK,
        int candidateChunkCount,
        int returnedChunkCount,
        double averageSimilarity,
        double durationMs,
        bool success,
        string? failureType = null)
    {
    }

    public void TrackAnswerGeneration(
        string provider,
        string deployment,
        int returnedChunkCount,
        int citationCount,
        double durationMs,
        int? inputTokenCount,
        int? outputTokenCount,
        bool success,
        string outcome,
        string? failureType = null)
    {
    }

    public void TrackRagQuery(
        int inputCharacterCount,
        int topK,
        int retrievedChunkCount,
        int contextLength,
        int promptLength,
        double durationMs,
        bool success)
    {
    }
}