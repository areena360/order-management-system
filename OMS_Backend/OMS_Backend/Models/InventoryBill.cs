public class InventoryBill : BaseEntity
{
    public int OrderId { get; set; }
    public string BillDetails { get; set; }
    public int? BillNumber { get; set; }
    public string? BillImage { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public Order Order { get; set; }
}