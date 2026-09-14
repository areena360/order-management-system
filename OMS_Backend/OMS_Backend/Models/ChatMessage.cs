namespace OMS_Backend.Models
{
    public class ChatMessage
    {
        public int Id { get; set; }

        /// <summary>
        /// The order this conversation belongs to.
        /// Each order has its own separate chat.
        /// </summary>
        public int OrderId { get; set; }

        /// <summary>
        /// The customer who owns the order. Kept for quick authorization checks.
        /// </summary>
        public int CustomerId { get; set; }

        /// <summary>
        /// The user who sent this message (admin or customer).
        /// </summary>
        public int SenderUserId { get; set; }

        /// <summary>
        /// "Customer" or "Admin". Used for display.
        /// </summary>
        public string SenderRole { get; set; } = "Customer";

        public string Message { get; set; } = string.Empty;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public bool IsDeleted { get; set; } = false;

        public Order? Order { get; set; }
        public User? Customer { get; set; }
        public User? Sender { get; set; }
    }
}