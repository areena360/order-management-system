public class OrderImage : BaseEntity
{
    public int OrderId { get; set; }
    public string ImageURL { get; set; }
    public Order Order { get; set; }
}