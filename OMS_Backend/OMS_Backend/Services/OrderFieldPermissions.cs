using Microsoft.EntityFrameworkCore;
using OMS_Backend.Data;

namespace OMS_Backend.Services;

public static class OrderFieldPermissions
{
    public static Task<bool> CanEditDeadlineAsync(OMSDbContext db, int userId) =>
        db.Users.AnyAsync(u => u.Id == userId && u.IsActive && !u.IsDeleted &&
            u.Role != null && u.Role.IsActive && (u.Role.Name == "Super Admin" || u.Role.Name == "Admin"));

    public static readonly string[] Keys = { "Order Amount", "Order Tracking" };
    public static bool DefaultEdit(string? role, string key) => role == "Super Admin" ||
        (key == "Order Amount" && (role is "Admin" or "Finance")) ||
        (key == "Order Tracking" && (role is "Admin" or "Staff"));

    public static bool CanEdit(string? role, string key, RolePermission? permission) =>
        role == "Super Admin" || (role != "Customer" &&
        (permission == null ? DefaultEdit(role, key) :
            permission.IsActive && !permission.IsDeleted && permission.CanView && permission.CanEdit));

    public static async Task<bool> CanEditAsync(OMSDbContext db, int userId, string key)
    {
        var user = await db.Users.Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive && !u.IsDeleted);
        if (user?.Role == null || !user.Role.IsActive) return false;
        var permission = await db.RolePermissions.FirstOrDefaultAsync(p => p.RoleId == user.RoleId && p.ScreenKey == key);
        return CanEdit(user.Role.Name, key, permission);
    }
}
