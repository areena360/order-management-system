namespace OMS_Backend.Models;

public class AdminOrderAssignment
{
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    // Retain the role at assignment time so changing a user's role revokes that assignment.
    public int RoleId { get; set; }
    public int AssignedByUserId { get; set; }
    public DateTime AssignedAt { get; set; }
    public string Status { get; set; } = "assigned";
    [System.ComponentModel.DataAnnotations.MaxLength(4000)]
    public string? Message { get; set; }
    public DateTime? MessageReadAt { get; set; }
}
