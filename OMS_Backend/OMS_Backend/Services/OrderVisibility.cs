using System.Linq.Expressions;

namespace OMS_Backend.Services;

public static class OrderVisibility
{
    public static Expression<Func<Order, bool>> ForUser(int userId, bool isCustomer) =>
        order => !order.IsDeleted && (isCustomer
            ? order.CustomerId == userId
            : !order.RequiresCustomerAssignment || order.IsAssigned);

    public static bool CanAccess(Order order, int userId, bool isCustomer) =>
        !order.IsDeleted && (isCustomer ? order.CustomerId == userId
            : !order.RequiresCustomerAssignment || order.IsAssigned);
}
