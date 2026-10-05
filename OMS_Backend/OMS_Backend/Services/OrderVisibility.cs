using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using OMS_Backend.Data;

namespace OMS_Backend.Services;

public static class OrderVisibility
{
    public static IQueryable<User> RestrictedUsers(OMSDbContext db) => db.Users.Where(u =>
        u.Role != null && u.Role.Name != "Super Admin" && u.Role.Name != "Customer" &&
        db.RolePermissions.Any(p => p.RoleId == u.RoleId && p.ScreenKey == "Orders"
            && p.IsActive && !p.IsDeleted && p.AdminAssignedOnly));

    public static Task<bool> IsRestrictedAsync(OMSDbContext db, int userId) =>
        RestrictedUsers(db).AnyAsync(u => u.Id == userId);

    public static Expression<Func<Order, bool>> ForUser(OMSDbContext db, int userId, bool isCustomer)
    {
        var restrictedUsers = RestrictedUsers(db);
        var viewers = OrderAccess.Viewers(db);
        var customers = viewers.Where(u => u.Role!.Name == "Customer");
        return order => viewers.Any(u => u.Id == userId) && !order.IsDeleted && (customers.Any(u => u.Id == userId) ? order.CustomerId == userId
            : (!order.RequiresCustomerAssignment || order.IsAssigned)
              && (!restrictedUsers.Any(u => u.Id == userId)
                  || db.AdminOrderAssignments.Any(a => a.OrderId == order.Id && a.UserId == userId
                      && a.User.RoleId == a.RoleId && a.User.IsActive && !a.User.IsDeleted
                      && a.User.Role != null && a.User.Role.IsActive)));
    }

    public static Expression<Func<Order, bool>> ForUser(int userId, bool isCustomer) =>
        order => !order.IsDeleted && (isCustomer
            ? order.CustomerId == userId
            : !order.RequiresCustomerAssignment || order.IsAssigned);

    public static bool CanAccess(Order order, int userId, bool isCustomer) =>
        !order.IsDeleted && (isCustomer ? order.CustomerId == userId
            : !order.RequiresCustomerAssignment || order.IsAssigned);
}
