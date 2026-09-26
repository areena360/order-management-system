using Microsoft.EntityFrameworkCore;
using OMS_Backend.Data;

namespace OMS_Backend.Services;

public static class ChatAccess
{
    public const string CustomerScreen = "Order Customer Chat";
    public const string GroupScreen = "Order Group Chat";

    public static IQueryable<User> AllowedUsers(OMSDbContext db, bool group, bool send = false)
    {
        var screen = group ? GroupScreen : CustomerScreen;
        return db.Users.Where(u => !u.IsDeleted && u.IsActive && u.Role != null
            && (!group || u.Role.Name != "Customer")
            && (u.Role.Name == "Super Admin"
                || db.RolePermissions.Any(p => p.RoleId == u.RoleId && p.ScreenKey == screen
                    && !p.IsDeleted && p.IsActive && p.CanView && (!send || p.CanAdd))
                // Existing customer conversations stay enabled until explicitly configured.
                || (!group && !db.RolePermissions.Any(p => p.RoleId == u.RoleId && p.ScreenKey == screen))));
    }

    public static Task<bool> CanAccess(OMSDbContext db, int userId, bool group, bool send = false)
        => AllowedUsers(db, group, send).AnyAsync(u => u.Id == userId);
}
