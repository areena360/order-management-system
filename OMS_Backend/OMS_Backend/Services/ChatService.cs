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

        // ================== Customer <-> Admin ==================
        public async Task<List<ChatUnreadDto>> GetUnreadAsync(int userId, bool isCustomer)
        {
            isCustomer = await IsCustomerAsync(userId);
            var groupAllowed = !isCustomer && await CanUserAccessGroupChatAsync(userId);
            var customerAllowed = await ChatAccess.CanAccess(_db, userId, false);
            var orders = _db.Orders.Where(OrderVisibility.ForUser(userId, isCustomer))
                .Where(o => !o.RequiresCustomerAssignment || o.IsAssigned).Select(o => o.Id);
            return await _db.ChatMessages.AsNoTracking()
                .Where(m => !m.IsDeleted && m.SenderUserId != userId && orders.Contains(m.OrderId)
                    && ((customerAllowed && m.Channel == "Customer") || (groupAllowed && m.Channel == "Group"))
                    && !_db.ChatReadStates.Any(r => r.UserId == userId && r.OrderId == m.OrderId
                        && r.Channel == m.Channel && r.LastReadMessageId >= m.Id))
                .GroupBy(m => new { m.OrderId, m.Channel })
                .Select(g => new ChatUnreadDto(g.Key.OrderId, g.Key.Channel, g.Count()))
                .ToListAsync();
        }

        public async Task MarkReadAsync(int orderId, int userId, bool isCustomer, MarkChatReadDto dto)
        {
            isCustomer = await IsCustomerAsync(userId);
            if (dto.Channel != "Customer" && dto.Channel != "Group")
                throw new ValidationAppException("Invalid chat channel.");
            if (!await ChatAccess.CanAccess(_db, userId, dto.Channel == "Group"))
                throw new ForbiddenAppException("You cannot view this chat.");
            if (!await _db.Orders.Where(OrderVisibility.ForUser(userId, isCustomer))
                .AnyAsync(o => o.Id == orderId && (!o.RequiresCustomerAssignment || o.IsAssigned)))
                throw new NotFoundException(nameof(Order), orderId);
            if (!await _db.ChatMessages.AnyAsync(m => m.Id == dto.LastReadMessageId
                && m.OrderId == orderId && m.Channel == dto.Channel && !m.IsDeleted))
                throw new ValidationAppException("Invalid read message.");
            // Atomic monotonic cursor keeps simultaneous tabs from reversing read progress.
            await _db.Database.ExecuteSqlInterpolatedAsync($@"
                MERGE [ChatReadStates] WITH (HOLDLOCK) AS target
                USING (SELECT {userId} AS UserId, {orderId} AS OrderId, {dto.Channel} AS Channel) AS source
                ON target.UserId = source.UserId AND target.OrderId = source.OrderId AND target.Channel = source.Channel
                WHEN MATCHED AND target.LastReadMessageId < {dto.LastReadMessageId}
                    THEN UPDATE SET LastReadMessageId = {dto.LastReadMessageId}
                WHEN NOT MATCHED THEN INSERT (UserId, OrderId, Channel, LastReadMessageId)
                    VALUES ({userId}, {orderId}, {dto.Channel}, {dto.LastReadMessageId});");
        }

        public async Task<ChatMessageDto> SaveMessageAsync(
            int orderId, int senderUserId, string senderRole, bool isCustomer, string message)
        {
            isCustomer = await IsCustomerAsync(senderUserId);
            if (!await ChatAccess.CanAccess(_db, senderUserId, false, true)) throw new ForbiddenAppException("You cannot send customer chat messages.");
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

            if (isCustomer && order.CustomerId != senderUserId)
                throw new NotFoundException(nameof(Order), orderId);

            var entity = new ChatMessage
            {
                OrderId = order.Id,
                CustomerId = order.CustomerId,
                SenderUserId = senderUserId,
                SenderRole = isCustomer ? "Customer" : "Admin",
                Message = message,
                Channel = "Customer",
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
                Channel = entity.Channel,
                CreatedDate = entity.CreatedDate
            };
        }

        public async Task<List<ChatMessageDto>> GetConversationAsync(
            int orderId, int currentUserId, bool isCustomer)
        {
            isCustomer = await IsCustomerAsync(currentUserId);
            if (!await ChatAccess.CanAccess(_db, currentUserId, false)) throw new ForbiddenAppException("You cannot view customer chat.");
            var order = await _db.Orders
                .Where(OrderVisibility.ForUser(currentUserId, isCustomer))
                .Where(o => o.Id == orderId && (!o.RequiresCustomerAssignment || o.IsAssigned))
                .Select(o => new { o.Id, o.CustomerId })
                .FirstOrDefaultAsync()
                ?? throw new NotFoundException(nameof(Order), orderId);

            if (isCustomer && order.CustomerId != currentUserId)
                throw new NotFoundException(nameof(Order), orderId);

            return await _db.ChatMessages
                .Where(m => m.OrderId == orderId
                         && !m.IsDeleted
                         && m.Channel == "Customer")
                .OrderBy(m => m.CreatedDate)
                .Select(m => new ChatMessageDto
                {
                    Id = m.Id,
                    OrderId = m.OrderId,
                    CustomerId = m.CustomerId,
                    SenderUserId = m.SenderUserId,
                    SenderRole = m.SenderRole,
                    Message = m.Message,
                    Channel = m.Channel,
                    CreatedDate = m.CreatedDate
                })
                .ToListAsync();
        }

        // ================== Staff Group Chat ==================
        public async Task<ChatMessageDto> SaveGroupMessageAsync(
            int orderId, int senderUserId, string senderRole, string message)
        {
            if (!await ChatAccess.CanAccess(_db, senderUserId, true, true)) throw new ForbiddenAppException("You cannot send group chat messages.");
            message = (message ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(message))
                throw new ValidationAppException("Message cannot be empty.");
            if (message.Length > 4000)
                throw new ValidationAppException("Message is too long.");

            var order = await _db.Orders
                .Where(OrderVisibility.ForUser(senderUserId, false))
                .Where(o => o.Id == orderId && (!o.RequiresCustomerAssignment || o.IsAssigned))
                .Select(o => new { o.Id, o.CustomerId })
                .FirstOrDefaultAsync()
                ?? throw new NotFoundException(nameof(Order), orderId);

            var entity = new ChatMessage
            {
                OrderId = order.Id,
                CustomerId = order.CustomerId,
                SenderUserId = senderUserId,
                SenderRole = string.IsNullOrWhiteSpace(senderRole) ? "Staff" : senderRole,
                Message = message,
                Channel = "Group",
                CreatedDate = DateTime.UtcNow,
                IsDeleted = false
            };

            _db.ChatMessages.Add(entity);
            await _db.SaveChangesAsync();

            // FIXED: use FirstName + LastName
            var senderName = await _db.Users
                .Where(u => u.Id == senderUserId)
                .Select(u => (u.FirstName + " " + u.LastName).Trim())
                .FirstOrDefaultAsync() ?? entity.SenderRole;

            return new ChatMessageDto
            {
                Id = entity.Id,
                OrderId = entity.OrderId,
                CustomerId = entity.CustomerId,
                SenderUserId = entity.SenderUserId,
                SenderRole = entity.SenderRole,
                SenderName = senderName,
                Message = entity.Message,
                Channel = entity.Channel,
                CreatedDate = entity.CreatedDate
            };
        }

        public async Task<List<ChatMessageDto>> GetGroupConversationAsync(int orderId, int userId)
        {
            if (!await ChatAccess.CanAccess(_db, userId, true)) throw new ForbiddenAppException("You cannot view group chat.");
            if (!await _db.Orders.Where(OrderVisibility.ForUser(userId, false)).AnyAsync(o => o.Id == orderId)) throw new NotFoundException(nameof(Order), orderId);
            return await _db.ChatMessages
                .Where(m => m.OrderId == orderId
                         && !m.IsDeleted
                         && m.Channel == "Group")
                .OrderBy(m => m.CreatedDate)
                .Select(m => new ChatMessageDto
                {
                    Id = m.Id,
                    OrderId = m.OrderId,
                    CustomerId = m.CustomerId,
                    SenderUserId = m.SenderUserId,
                    SenderRole = m.SenderRole,
                    // FIXED: use FirstName + LastName
                    SenderName = _db.Users
                        .Where(u => u.Id == m.SenderUserId)
                        .Select(u => (u.FirstName + " " + u.LastName).Trim())
                        .FirstOrDefault() ?? m.SenderRole,
                    Message = m.Message,
                    Channel = m.Channel,
                    CreatedDate = m.CreatedDate
                })
                .ToListAsync();
        }

        public async Task<List<string>> GetGroupChatAllowedRolesAsync()
        {
            return await _db.RolePermissions
                .Where(p => p.ScreenKey == "Order Group Chat"
                         && p.CanView
                         && p.IsActive
                         && !p.IsDeleted)
                .Join(_db.Roles, p => p.RoleId, r => r.Id, (p, r) => r.Name)
                .Distinct()
                .ToListAsync();
        }

        private Task<bool> IsCustomerAsync(int userId) => _db.Users.AnyAsync(u => u.Id == userId && u.Role != null && u.Role.Name == "Customer");

        public Task<bool> CanUserAccessGroupChatAsync(int userId) => ChatAccess.CanAccess(_db, userId, true);

        public async Task<List<string>> GetRecipientGroupsAsync(int orderId, bool group)
        {
            var customerId = await _db.Orders.Where(o => o.Id == orderId).Select(o => o.CustomerId).SingleAsync();
            var ids = await ChatAccess.AllowedUsers(_db, group)
                .Where(u => u.Role!.Name != "Customer" || u.Id == customerId)
                .Select(u => u.Id).ToListAsync();
            return ids.Select(id => $"user_{id}").ToList();
        }
    }
}
