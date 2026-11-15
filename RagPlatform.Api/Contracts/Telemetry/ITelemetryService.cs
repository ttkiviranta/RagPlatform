namespace RagPlatform.Api.Contracts.Telemetry;

public interface ITelemetryService
{
    void TrackApiRequest(string route, int statusCode, double durationMs, string method);

    void TrackDatabaseQuery(string operation, double durationMs, bool success);

    void TrackDocumentProcessing(string operation, double durationMs, bool success, int itemCount = 0);

    void TrackDocumentChunking(double durationMs, bool success, int generatedChunkCount, int reusedChunkCount);

    void TrackEmbeddingGeneration(string modelName, double durationMs, int vectorDimensions, bool success);

    void TrackEmbeddingBatch(
        string modelName,
        double durationMs,
        bool success,
        int generatedEmbeddingCount,
        int reusedEmbeddingCount);

    void TrackVectorSearch(
        string modelName,
        int topK,
        int candidateChunkCount,
        int returnedChunkCount,
        double averageSimilarity,
        double durationMs,
        bool success,
        string? failureType = null);

    void TrackAnswerGeneration(
        string provider,
        string deployment,
        int returnedChunkCount,
        int citationCount,
        double durationMs,
        int? inputTokenCount,
        int? outputTokenCount,
        bool success,
        string outcome,
        string? failureType = null);

    void TrackRagQuery(int inputCharacterCount, int topK, int retrievedChunkCount, int contextLength, int promptLength, double durationMs, bool success);
}
