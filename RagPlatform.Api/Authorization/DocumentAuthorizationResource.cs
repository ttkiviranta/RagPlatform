namespace RagPlatform.Api.Authorization;

public sealed record DocumentAuthorizationResource(Guid? OwnerUserId, string TenantId);
