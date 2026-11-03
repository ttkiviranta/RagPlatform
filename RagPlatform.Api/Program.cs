using System.Reflection;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Pgvector.EntityFrameworkCore;
using RagPlatform.Api.Contracts.Chunking;
using RagPlatform.Api.Contracts.Documents;
using RagPlatform.Api.Contracts.Embeddings;
using RagPlatform.Api.Contracts.Health;
using RagPlatform.Api.Contracts.Processing;
using RagPlatform.Api.Contracts.Search;
using RagPlatform.Api.Contracts.Telemetry;
using RagPlatform.Api.Contracts.Web;
using RagPlatform.Api.Data;
using RagPlatform.Api.Providers;
using RagPlatform.Api.Services;

var builder = WebApplication.CreateBuilder(args);

var appInsightsConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"]
    ?? Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");

if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
{
    builder.Services.AddApplicationInsightsTelemetry(options =>
    {
        options.ConnectionString = appInsightsConnectionString;
    });
}
else
{
    builder.Services.AddSingleton(new TelemetryClient(new TelemetryConfiguration()));
    builder.Logging.AddConsole();
}

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpClient("WebContentClient", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
});
builder.Services.AddSwaggerGen(options =>
{
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);

    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(serviceName: "RagPlatform.Api", serviceVersion: "1.0.0"))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddSqlClientInstrumentation();

        var otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            tracing.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
        }
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation();

        var otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            metrics.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
        }
    });

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    var host = Environment.GetEnvironmentVariable("RAGPOSTGRES_HOST") ?? "localhost";
    var port = Environment.GetEnvironmentVariable("RAGPOSTGRES_PORT") ?? "5432";
    var database = Environment.GetEnvironmentVariable("RAGPOSTGRES_DB") ?? "ragplatform";
    var username = Environment.GetEnvironmentVariable("RAGPOSTGRES_USER") ?? "postgres";
    var password = Environment.GetEnvironmentVariable("RAGPOSTGRES_PASSWORD");

    connectionString = $"Host={host};Port={port};Database={database};Username={username};";
    if (!string.IsNullOrWhiteSpace(password))
    {
        connectionString += $"Password={password};";
    }
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString, npgsqlOptions => npgsqlOptions.UseVector()));

builder.Services.AddScoped<IHealthService, HealthService>();
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<IPlainTextProcessor, PlainTextProcessor>();
builder.Services.AddScoped<IMarkdownProcessor, MarkdownProcessor>();
builder.Services.AddScoped<IWebContentService, WebContentService>();
builder.Services.AddScoped<IDocumentChunkingService, DocumentChunkingService>();
builder.Services.AddScoped<IEmbeddingProvider, PlaceholderEmbeddingProvider>();
builder.Services.AddScoped<IEmbeddingService, EmbeddingService>();
builder.Services.AddScoped<IVectorSearchService, VectorSearchService>();
builder.Services.AddScoped<ITelemetryService, ApplicationInsightsTelemetryService>();
builder.Services.AddScoped<DocumentProcessingService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "RAG Platform API v1");
    });
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();

