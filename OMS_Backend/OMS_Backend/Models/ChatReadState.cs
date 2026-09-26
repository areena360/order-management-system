namespace OMS_Backend.Models;
public class ChatReadState
{
    public int UserId { get; set; }
    public int OrderId { get; set; }
    public string Channel { get; set; } = "Customer";
    public int LastReadMessageId { get; set; }
}
