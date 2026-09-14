using OMS_Backend.DTOs;

namespace OMS_Backend.Services
{
    public interface IChatService
    {
        Task<ChatMessageDto> SaveMessageAsync(
            int orderId,
            int senderUserId,
            string senderRole,
            bool isCustomer,
            string message);

        Task<List<ChatMessageDto>> GetConversationAsync(
            int orderId,
            int currentUserId,
            bool isCustomer);
    }
}