namespace RagPlatform.Api.Contracts.Audit;

public static class AuditEventTypes
{
    public const string AuthenticationTokenValidated = "AuthenticationTokenValidated";
    public const string UserBootstrapped = "UserBootstrapped";
    public const string ActiveUserAuthorizationDenied = "ActiveUserAuthorizationDenied";
    public const string DocumentCreated = "DocumentCreated";
    public const string DocumentUpdated = "DocumentUpdated";
    public const string DocumentDeleted = "DocumentDeleted";
    public const string DocumentAccessDenied = "DocumentAccessDenied";
    public const string DocumentImportStarted = "DocumentImportStarted";
    public const string DocumentImportMessagePublishFailed = "DocumentImportMessagePublishFailed";
    public const string DocumentImportExtractionSucceeded = "DocumentImportExtractionSucceeded";
    public const string DocumentImportPermanentPdfFailure = "DocumentImportPermanentPdfFailure";
    public const string DocumentChunksGenerated = "DocumentChunksGenerated";
    public const string DocumentEmbeddingsGenerated = "DocumentEmbeddingsGenerated";
    public const string ConversationCreated = "ConversationCreated";
    public const string ConversationDeleted = "ConversationDeleted";
    public const string ConversationAccessDenied = "ConversationAccessDenied";
    public const string VectorSearchExecuted = "VectorSearchExecuted";
    public const string RagQueryExecuted = "RagQueryExecuted";
    public const string FoundryModelInvocation = "FoundryModelInvocation";
}
