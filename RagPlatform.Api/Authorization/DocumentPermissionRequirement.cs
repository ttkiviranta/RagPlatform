using Microsoft.AspNetCore.Authorization;

namespace RagPlatform.Api.Authorization;

public sealed class DocumentPermissionRequirement(DocumentPermission permission) : IAuthorizationRequirement
{
    public DocumentPermission Permission { get; } = permission;
}
