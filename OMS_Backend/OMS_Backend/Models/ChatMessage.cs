namespace OMS_Backend.Models
{
    public class ChatMessage
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public int CustomerId { get; set; }
        public int SenderUserId { get; set; }
        public string SenderRole { get; set; } = "Customer";
        public string Message { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; } = false;

        // NEW: "Customer" (customer<->admin) | "Group" (staff internal)
        public string Channel { get; set; } = "Customer";

        public Order? Order { get; set; }
        public User? Customer { get; set; }
        public User? Sender { get; set; }
    }
}