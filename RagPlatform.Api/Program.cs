using System.Reflection;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using System.Security.Claims;
using Azure.Core;
using Azure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Pgvector.EntityFrameworkCore;
using RagPlatform.Api.Authorization;
using RagPlatform.Api.Contracts.Audit;
using RagPlatform.Api.Contracts.Authorization;
using RagPlatform.Api.Contracts.Chunking;
using RagPlatform.Api.Contracts.Conversations;
using RagPlatform.Api.Contracts.Documents;
using RagPlatform.Api.Contracts.Embeddings;
using RagPlatform.Api.Contracts.Health;
using RagPlatform.Api.Contracts.Identity;
using RagPlatform.Api.Contracts.Processing;
using RagPlatform.Api.Contracts.Rag;
using RagPlatform.Api.Contracts.Search;
using RagPlatform.Api.Contracts.Telemetry;
using RagPlatform.Api.Contracts.Web;
using RagPlatform.Api.Data;
using RagPlatform.Api.Options;
using RagPlatform.Api.Providers;
using RagPlatform.Api.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<FoundryOptions>(
    builder.Configuration.GetSection(FoundryOptions.SectionName));
builder.Services.AddSingleton<TokenCredential>(_ => new DefaultAzureCredential());
builder.Services.AddHttpClient("FoundryInference", client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
});
builder.Services.AddScoped<FoundryInferenceClient>();

var azureAdAuthority = builder.Configuration["AzureAd:Authority"]
    ?? $"https://login.microsoftonline.com/{builder.Configuration["AzureAd:TenantId"]}";
var azureAdAudience = builder.Configuration["AzureAd:Audience"]
    ?? $"api://{builder.Configuration["AzureAd:ClientId"]}";

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
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = azureAdAuthority;
        options.Audience = azureAdAudience;
        options.RequireHttpsMetadata = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = azureAdAuthority,
            ValidateAudience = true,
            ValidAudience = azureAdAudience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            NameClaimType = ClaimTypes.NameIdentifier,
            RoleClaimType = ClaimTypes.Role,
            ClockSkew = TimeSpan.FromMinutes(2)
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var principal = context.Principal;
                var tenantId = principal?.FindFirst("tid")?.Value
                    ?? principal?.FindFirst("http://schemas.microsoft.com/identity/claims/tenantid")?.Value;
                if (string.IsNullOrWhiteSpace(tenantId))
                {
                    var logger = context.HttpContext.RequestServices
                        .GetRequiredService<ILoggerFactory>()
                        .CreateLogger("AuthenticationAudit");
                    logger.LogWarning("A validated token did not contain a tenant identifier; no audit event was written.");
                    return;
                }

                var entraObjectId = principal?.FindFirst("oid")?.Value
                    ?? principal?.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;
                var dbContext = context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
                Guid? actorUserId = null;
                if (!string.IsNullOrWhiteSpace(entraObjectId))
                {
                    actorUserId = await dbContext.Users
                        .Where(user => user.EntraObjectId == entraObjectId && user.TenantId == tenantId)
                        .Select(user => (Guid?)user.Id)
                        .FirstOrDefaultAsync(context.HttpContext.RequestAborted);
                }

                var auditEventWriter = context.HttpContext.RequestServices.GetRequiredService<IAuditEventWriter>();
                auditEventWriter.Add(
                    actorUserId,
                    tenantId,
                    AuditEventTypes.AuthenticationTokenValidated,
                    "Succeeded",
                    "Authentication");
                await dbContext.SaveChangesAsync(context.HttpContext.RequestAborted);
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthorizationPolicies.ActiveUser, policy =>
        policy.RequireAuthenticatedUser()
            .AddRequirements(new ActiveUserRequirement()));
    options.AddPolicy(AuthorizationPolicies.DocumentRead, policy =>
        policy.RequireAuthenticatedUser()
            .AddRequirements(new ActiveUserRequirement(), new DocumentPermissionRequirement(DocumentPermission.Read)));
    options.AddPolicy(AuthorizationPolicies.DocumentWrite, policy =>
        policy.RequireAuthenticatedUser()
            .AddRequirements(new ActiveUserRequirement(), new DocumentPermissionRequirement(DocumentPermission.Write)));
    options.AddPolicy(AuthorizationPolicies.DocumentDelete, policy =>
        policy.RequireAuthenticatedUser()
            .AddRequirements(new ActiveUserRequirement(), new DocumentPermissionRequirement(DocumentPermission.Delete)));
    options.AddPolicy(AuthorizationPolicies.UserRoleManage, policy =>
        policy.RequireAuthenticatedUser()
            .AddRequirements(new ActiveUserRequirement(), new UserRoleManagementRequirement()));
});
builder.Services.AddScoped<IAuthorizationHandler, ActiveUserAuthorizationHandler>();
builder.Services.AddScoped<IAuthorizationHandler, DocumentAuthorizationHandler>();
builder.Services.AddScoped<IAuthorizationHandler, UserRoleManagementAuthorizationHandler>();
builder.Services.AddHttpContextAccessor();
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
builder.Services.AddScoped<IAuditEventWriter, AuditEventWriter>();
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<IConversationService, ConversationService>();
builder.Services.AddScoped<IConversationMessageService, ConversationMessageService>();
builder.Services.AddScoped<IPlainTextProcessor, PlainTextProcessor>();
builder.Services.AddScoped<IMarkdownProcessor, MarkdownProcessor>();
builder.Services.AddScoped<IWebContentService, WebContentService>();
builder.Services.AddScoped<IDocumentChunkingService, DocumentChunkingService>();
builder.Services.AddScoped<IEmbeddingProvider, FoundryEmbeddingProvider>();
builder.Services.AddScoped<IEmbeddingService, EmbeddingService>();
builder.Services.AddScoped<IVectorSearchService, VectorSearchService>();
builder.Services.AddScoped<IContextBuilder, ContextBuilder>();
builder.Services.AddScoped<IPromptBuilder, PromptBuilder>();
builder.Services.AddScoped<IRagService, RagService>();
builder.Services.AddScoped<IFoundryChatCompletionService, FoundryChatCompletionService>();
builder.Services.AddScoped<ITelemetryService, ApplicationInsightsTelemetryService>();
builder.Services.AddScoped<IUserBootstrapService, UserBootstrapService>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IDocumentAccessService, DocumentAccessService>();
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
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
