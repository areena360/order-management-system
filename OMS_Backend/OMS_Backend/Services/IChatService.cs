using OMS_Backend.DTOs;

namespace OMS_Backend.Services
{
    public interface IChatService
    {
        Task<List<ChatUnreadDto>> GetUnreadAsync(int userId, bool isCustomer);
        Task MarkReadAsync(int orderId, int userId, bool isCustomer, MarkChatReadDto dto);
        // Customer <-> Admin
        Task<ChatMessageDto> SaveMessageAsync(
            int orderId, int senderUserId, string senderRole, bool isCustomer, string message);

        Task<List<ChatMessageDto>> GetConversationAsync(
            int orderId, int currentUserId, bool isCustomer);

        // Staff group chat
        Task<ChatMessageDto> SaveGroupMessageAsync(
            int orderId, int senderUserId, string senderRole, string message);

        Task<List<ChatMessageDto>> GetGroupConversationAsync(int orderId, int userId);

        Task<List<string>> GetGroupChatAllowedRolesAsync();
        Task<List<string>> GetRecipientGroupsAsync(int orderId, bool group);
        Task<bool> CanUserAccessGroupChatAsync(int userId);
    }
}
