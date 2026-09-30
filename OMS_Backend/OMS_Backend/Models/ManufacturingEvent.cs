namespace OMS_Backend.Models;

public class ManufacturingEvent
{
    public long Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public int UserId { get; set; }
    public string UserName { get; set; } = "";
    public int RoleId { get; set; }
    public string RoleName { get; set; } = "";
    public string Status { get; set; } = "assigned";
    public DateTime OccurredAt { get; set; }
    public bool IsSnapshot { get; set; }
}
