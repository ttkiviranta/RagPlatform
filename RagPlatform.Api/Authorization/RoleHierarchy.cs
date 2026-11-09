using RagPlatform.Api.Models;

namespace RagPlatform.Api.Authorization;

public static class RoleHierarchy
{
    public static bool HasRole(User user, string roleName)
    {
        return user.UserRoles.Any(userRole => userRole.RoleName == roleName);
    }

    public static bool CanManageTenantDocuments(User user)
    {
        return HasRole(user, RoleNames.PowerUser) || HasRole(user, RoleNames.SuperUser);
    }

    public static bool CanAccessOwnedDocuments(User user)
    {
        return HasRole(user, RoleNames.User) || CanManageTenantDocuments(user);
    }

    public static bool CanManageUserRoles(User user)
    {
        return HasRole(user, RoleNames.SuperUser);
    }
}
