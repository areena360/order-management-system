namespace OMS_Backend.DTOs
{
    public class ChatMessageDto
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public int CustomerId { get; set; }
        public int SenderUserId { get; set; }
        public string SenderRole { get; set; } = "Customer";
        public string SenderName { get; set; } = string.Empty;  // NEW (group chat ke liye)
        public string Message { get; set; } = string.Empty;
        public string Channel { get; set; } = "Customer";       // NEW
        public DateTime CreatedDate { get; set; }
    }
}