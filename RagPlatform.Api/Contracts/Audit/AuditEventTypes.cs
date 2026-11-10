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
    public const string ConversationCreated = "ConversationCreated";
    public const string ConversationDeleted = "ConversationDeleted";
    public const string ConversationAccessDenied = "ConversationAccessDenied";
    public const string VectorSearchExecuted = "VectorSearchExecuted";
    public const string RagQueryExecuted = "RagQueryExecuted";
}
