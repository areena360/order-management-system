public class Order : BaseEntity
{
    public string CustomerProductTitle { get; set; }
    public string? ManufacturerProductTitle { get; set; }
    public string? CustomerOrderNumber { get; set; }
    public string ManufacturerOrderNumber { get; set; } // Auto-generated AD1001...
    public int CustomerId { get; set; }
    public int? Amount { get; set; }
    public int GenderId { get; set; }
    public int CustomerMaterialId { get; set; }
    public int ManufacturerMaterialId { get; set; }
    public bool IsCustomSize { get; set; }
    public string? SizeDetails { get; set; }
    public int? SizeId { get; set; }
    public int? SizeChartId { get; set; }
    public int OrderStatusId { get; set; }
    public string ConsigneeName { get; set; }
    public string ConsigneeAddress { get; set; }
    public string? TrackingNumber { get; set; }
    public string? NotesByCustomer { get; set; }
    public string? NotesByManufacturer { get; set; }
    public int DaysForMaking { get; set; }
    public int? PriorityId { get; set; }

    public User Customer { get; set; }
    public ICollection<OrderImage> OrderImages { get; set; }
    public ICollection<OrderStatusHistory> StatusHistories { get; set; }
    public ICollection<InventoryBill> InventoryBills { get; set; }
}