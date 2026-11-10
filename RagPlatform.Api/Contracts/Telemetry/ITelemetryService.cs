namespace RagPlatform.Api.Contracts.Telemetry;

public interface ITelemetryService
{
    void TrackApiRequest(string route, int statusCode, double durationMs, string method);

    void TrackDatabaseQuery(string operation, double durationMs, bool success);

    void TrackDocumentProcessing(string operation, double durationMs, bool success, int itemCount = 0);

    void TrackEmbeddingGeneration(string modelName, double durationMs, int vectorDimensions, bool success);

    void TrackVectorSearch(string modelName, int topK, int resultCount, double averageSimilarity, double durationMs, bool success);

    void TrackRagQuery(int inputCharacterCount, int topK, int retrievedChunkCount, int contextLength, int promptLength, double durationMs, bool success);
}
