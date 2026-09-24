using System.Linq.Expressions;

namespace OMS_Backend.Services;

public static class OrderStatusCatalog
{
    public const int LookupTypeId = 1;
    public const string Assign = "Assign";
    public const string InManufacturing = "In Manufacturing";
    public const string Refund = "Refund";
    public const string Cancel = "Cancel";
    public static readonly string[] Names = [Assign, InManufacturing, Refund, Cancel];

    public static readonly Expression<Func<LookupItem, bool>> Selectable = item =>
        item.LookupDataTypeId == LookupTypeId && item.IsActive && !item.IsDeleted && Names.Contains(item.Name);
}
