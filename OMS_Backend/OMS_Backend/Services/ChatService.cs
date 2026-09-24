using Microsoft.EntityFrameworkCore;
using OMS_Backend.Common.Exceptions;
using OMS_Backend.Data;
using OMS_Backend.DTOs;
using OMS_Backend.Models;

namespace OMS_Backend.Services
{
    public class ChatService : IChatService
    {
        private readonly OMSDbContext _db;

        public ChatService(OMSDbContext db)
        {
            _db = db;
        }

        public async Task<ChatMessageDto> SaveMessageAsync(
            int orderId,
            int senderUserId,
            string senderRole,
            bool isCustomer,
            string message)
        {
            message = (message ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(message))
                throw new ValidationAppException("Message cannot be empty.");

            if (message.Length > 4000)
                throw new ValidationAppException("Message is too long.");

            var order = await _db.Orders
                .Where(OrderVisibility.ForUser(senderUserId, isCustomer))
                .Where(o => o.Id == orderId && (!o.RequiresCustomerAssignment || o.IsAssigned))
                .Select(o => new { o.Id, o.CustomerId })
                .FirstOrDefaultAsync()
                ?? throw new NotFoundException(nameof(Order), orderId);

            // Customers may only message on their own orders.
            if (isCustomer && order.CustomerId != senderUserId)
                throw new NotFoundException(nameof(Order), orderId);

            var entity = new ChatMessage
            {
                OrderId = order.Id,
                CustomerId = order.CustomerId,
                SenderUserId = senderUserId,
                SenderRole = isCustomer ? "Customer" : "Admin",
                Message = message,
                CreatedDate = DateTime.UtcNow,
                IsDeleted = false
            };

            _db.ChatMessages.Add(entity);
            await _db.SaveChangesAsync();

            return new ChatMessageDto
            {
                Id = entity.Id,
                OrderId = entity.OrderId,
                CustomerId = entity.CustomerId,
                SenderUserId = entity.SenderUserId,
                SenderRole = entity.SenderRole,
                Message = entity.Message,
                CreatedDate = entity.CreatedDate
            };
        }

        public async Task<List<ChatMessageDto>> GetConversationAsync(
            int orderId,
            int currentUserId,
            bool isCustomer)
        {
            var order = await _db.Orders
                .Where(OrderVisibility.ForUser(currentUserId, isCustomer))
                .Where(o => o.Id == orderId && (!o.RequiresCustomerAssignment || o.IsAssigned))
                .Select(o => new { o.Id, o.CustomerId })
                .FirstOrDefaultAsync()
                ?? throw new NotFoundException(nameof(Order), orderId);

            if (isCustomer && order.CustomerId != currentUserId)
                throw new NotFoundException(nameof(Order), orderId);

            return await _db.ChatMessages
                .Where(m => m.OrderId == orderId && !m.IsDeleted)
                .OrderBy(m => m.CreatedDate)
                .Select(m => new ChatMessageDto
                {
                    Id = m.Id,
                    OrderId = m.OrderId,
                    CustomerId = m.CustomerId,
                    SenderUserId = m.SenderUserId,
                    SenderRole = m.SenderRole,
                    Message = m.Message,
                    CreatedDate = m.CreatedDate
                })
                .ToListAsync();
        }
    }
}
