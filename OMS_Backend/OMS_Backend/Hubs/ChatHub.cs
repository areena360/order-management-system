using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using OMS_Backend.Services;
using System.Security.Claims;

namespace OMS_Backend.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly IChatService _chatService;

        public ChatHub(IChatService chatService)
        {
            _chatService = chatService;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = GetUserId();
            var isCustomer = IsCustomer();

            // Every connected user joins their personal group.
            // This lets the server target messages to a specific user
            // regardless of which page they have open.
            if (userId > 0)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
            }

            // Admins and staff also join a shared group so they receive
            // notifications for all conversations.
            if (!isCustomer)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, "staff");
            }

            await base.OnConnectedAsync();
        }

        /// <summary>
        /// Kept for compatibility. The chat now uses user-based and staff
        /// groups, so joining a per-order group is not required.
        /// </summary>
        public Task JoinOrderChat(int orderId) => Task.CompletedTask;

        public Task LeaveOrderChat(int orderId) => Task.CompletedTask;

        public async Task SendMessage(int orderId, string message)
        {
            var userId = GetUserId();
            var isCustomer = IsCustomer();

            var dto = await _chatService.SaveMessageAsync(
                orderId, userId, isCustomer ? "Customer" : "Admin", isCustomer, message);

            // Deliver to:
            //   - the customer's personal group (so their open tabs get it)
            //   - the shared staff group (so all admins get it)
            //
            // Frontend filters out the sender's own messages for badges,
            // and appends messages only for the matching order in the modal.
            await Task.WhenAll(
                Clients.Group($"user_{dto.CustomerId}").SendAsync("MessageReceived", dto),
                Clients.Group("staff").SendAsync("MessageReceived", dto)
            );
        }

        private int GetUserId()
        {
            var claim = Context.User?.FindFirst("userId")?.Value;
            return int.TryParse(claim, out var id) ? id : 0;
        }

        private bool IsCustomer()
        {
            var role = Context.User?.FindFirst(ClaimTypes.Role)?.Value
                ?? Context.User?.FindFirst("role")?.Value
                ?? Context.User?.FindFirst("Role")?.Value;

            return string.Equals(role, "Customer", StringComparison.OrdinalIgnoreCase);
        }
    }
}