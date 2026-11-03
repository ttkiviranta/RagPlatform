using Microsoft.ApplicationInsights;
using RagPlatform.Api.Contracts.Telemetry;

namespace RagPlatform.Api.Services;

public sealed class ApplicationInsightsTelemetryService : ITelemetryService
{
    private readonly TelemetryClient _telemetryClient;
    private readonly ILogger<ApplicationInsightsTelemetryService> _logger;

    public ApplicationInsightsTelemetryService(
        TelemetryClient telemetryClient,
        ILogger<ApplicationInsightsTelemetryService> logger)
    {
        _telemetryClient = telemetryClient;
        _logger = logger;
    }

    public void TrackApiRequest(string route, int statusCode, double durationMs, string method)
    {
        var properties = new Dictionary<string, string>
        {
            ["Route"] = route,
            ["Method"] = method,
            ["StatusCode"] = statusCode.ToString()
        };

        _telemetryClient.TrackEvent("ApiRequest", properties);
        _telemetryClient.TrackMetric("RequestDurationMs", durationMs, properties);
        _logger.LogInformation(
            "API request tracked: route={Route}, method={Method}, statusCode={StatusCode}, durationMs={DurationMs}",
            route,
            method,
            statusCode,
            durationMs);
    }

    public void TrackDatabaseQuery(string operation, double durationMs, bool success)
    {
        var properties = new Dictionary<string, string>
        {
            ["Operation"] = operation,
            ["Success"] = success.ToString()
        };

        _telemetryClient.TrackEvent("DatabaseQuery", properties);
        _telemetryClient.TrackMetric("QueryDurationMs", durationMs, properties);
    }

    public void TrackDocumentProcessing(string operation, double durationMs, bool success, int itemCount = 0)
    {
        var properties = new Dictionary<string, string>
        {
            ["Operation"] = operation,
            ["Success"] = success.ToString()
        };

        _telemetryClient.TrackEvent("DocumentProcessing", properties);
        _telemetryClient.TrackMetric("DocumentProcessingDurationMs", durationMs, properties);
        _telemetryClient.TrackMetric("ItemCount", itemCount, properties);
    }

    public void TrackEmbeddingGeneration(string modelName, double durationMs, int vectorDimensions, bool success)
    {
        var properties = new Dictionary<string, string>
        {
            ["ModelName"] = modelName,
            ["Success"] = success.ToString()
        };

        _telemetryClient.TrackEvent("EmbeddingGeneration", properties);
        _telemetryClient.TrackMetric("EmbeddingDurationMs", durationMs, properties);
        _telemetryClient.TrackMetric("VectorDimensions", vectorDimensions, properties);
    }

    public void TrackVectorSearch(string modelName, int topK, int resultCount, double averageSimilarity, double durationMs, bool success)
    {
        var properties = new Dictionary<string, string>
        {
            ["ModelName"] = modelName,
            ["Success"] = success.ToString()
        };

        _telemetryClient.TrackEvent("VectorSearch", properties);
        _telemetryClient.TrackMetric("VectorSearchDurationMs", durationMs, properties);
        _telemetryClient.TrackMetric("TopK", topK, properties);
        _telemetryClient.TrackMetric("ResultCount", resultCount, properties);
        _telemetryClient.TrackMetric("AverageSimilarity", averageSimilarity, properties);
    }
}
