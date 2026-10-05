using Microsoft.EntityFrameworkCore;
using OMS_Backend.Common.Exceptions;
using OMS_Backend.Data;

namespace OMS_Backend.Services;

public enum OrderAction { View, Add, Edit, Delete }

public sealed record OrderAccess(bool IsCustomer, bool AssignedOnly, bool CanView,
    bool CanAdd, bool CanEdit, bool CanDelete, bool CanSeeSensitiveData)
{
    public bool Allows(OrderAction action) => CanView && (action switch
    {
        OrderAction.View => true,
        OrderAction.Add => CanAdd,
        OrderAction.Edit => CanEdit,
        OrderAction.Delete => CanDelete,
        _ => false
    });

    public void Require(OrderAction action)
    {
        if (!Allows(action)) throw new ForbiddenAppException($"You do not have permission to {action.ToString().ToLowerInvariant()} orders.");
    }

    public static IQueryable<User> Viewers(OMSDbContext db) => db.Users.Where(u =>
        u.IsActive && !u.IsDeleted && u.Role != null && u.Role.IsActive && !u.Role.IsDeleted &&
        (u.Role.Name == "Super Admin" || db.RolePermissions.Any(p =>
            p.RoleId == u.RoleId && p.ScreenKey == "Orders" && p.IsActive && !p.IsDeleted &&
            (p.CanView || (p.AdminAssignedOnly && u.Role.Name != "Customer")))));

    public static async Task<OrderAccess> LoadAsync(OMSDbContext db, int userId)
    {
        // Read current database grants, not potentially stale role claims from a JWT.
        var user = await db.Users.AsNoTracking().Include(u => u.Role)
            .SingleOrDefaultAsync(u => u.Id == userId && u.IsActive && !u.IsDeleted);
        if (user?.Role == null || !user.Role.IsActive || user.Role.IsDeleted)
            return new(false, false, false, false, false, false, false);
        var role = user.Role.Name;
        if (role == "Super Admin") return new(false, false, true, true, true, true, true);
        var permission = await db.RolePermissions.AsNoTracking().SingleOrDefaultAsync(p =>
            p.RoleId == user.RoleId && p.ScreenKey == "Orders" && p.IsActive && !p.IsDeleted);
        var customer = role == "Customer";
        var restricted = !customer && permission?.AdminAssignedOnly == true;
        var normalView = !restricted && permission?.CanView == true;
        return new(customer, restricted, normalView || restricted,
            normalView && permission!.CanAdd, normalView && permission!.CanEdit,
            normalView && permission!.CanDelete, !restricted && (customer || role == "Admin"));
    }

    public static async Task<OrderAccess> RequireAsync(OMSDbContext db, int userId, OrderAction action)
    {
        var access = await LoadAsync(db, userId);
        access.Require(action);
        return access;
    }
}
