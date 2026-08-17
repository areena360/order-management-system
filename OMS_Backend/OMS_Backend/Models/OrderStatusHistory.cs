public class OrderStatusHistory : BaseEntity
{
    public int OrderId { get; set; }
    public int StatusId { get; set; }
    public Order Order { get; set; }
}