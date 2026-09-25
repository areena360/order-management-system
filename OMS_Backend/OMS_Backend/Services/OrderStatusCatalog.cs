using System.Linq.Expressions;

namespace OMS_Backend.Services;

public static class OrderStatusCatalog
{
    public const int LookupTypeId = 1;
    public const string Assign = "assign";
    public const string New = "new";
    public const string InManufacturing = "in manufacturing";
    public const string Completed = "completed";
    public const string Shipped = "shipped";
    public const string Refund = "refund";
    public const string Cancel = "cancel";
    public static readonly string[] Names = [New, Assign, InManufacturing, Completed, Shipped, Refund, Cancel];

    public static readonly Expression<Func<LookupItem, bool>> Selectable = item =>
        item.LookupDataTypeId == LookupTypeId && item.IsActive && !item.IsDeleted;
}
