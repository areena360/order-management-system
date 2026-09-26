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
            var role = GetRole();

            if (userId > 0)
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");

            if (!isCustomer)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, "staff");

                if (!string.IsNullOrWhiteSpace(role))
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"role_{role}");
            }

            await base.OnConnectedAsync();
        }

        public Task JoinOrderChat(int orderId) => Task.CompletedTask;
        public Task LeaveOrderChat(int orderId) => Task.CompletedTask;

        // -------- Customer <-> Admin --------
        public async Task SendMessage(int orderId, string message)
        {
            var userId = GetUserId();
            var isCustomer = IsCustomer();

            var dto = await _chatService.SaveMessageAsync(
                orderId, userId, isCustomer ? "Customer" : "Admin", isCustomer, message);

            var recipients = await _chatService.GetRecipientGroupsAsync(orderId, false);
            await Clients.Groups(recipients).SendAsync("MessageReceived", dto);
        }

        // -------- Group (staff only) --------
        public async Task SendGroupMessage(int orderId, string message)
        {
            var userId = GetUserId();
            if (IsCustomer())
                throw new HubException("Customers cannot send group messages.");

            var role = GetRole() ?? "Staff";

            var dto = await _chatService.SaveGroupMessageAsync(orderId, userId, role, message);

            var recipients = await _chatService.GetRecipientGroupsAsync(orderId, true);
            await Clients.Groups(recipients).SendAsync("GroupMessageReceived", dto);
        }

        private int GetUserId()
        {
            var claim = Context.User?.FindFirst("userId")?.Value;
            return int.TryParse(claim, out var id) ? id : 0;
        }

        private bool IsCustomer()
        {
            var role = GetRole();
            return string.Equals(role, "Customer", StringComparison.OrdinalIgnoreCase);
        }

        private string? GetRole()
        {
            return Context.User?.FindFirst(ClaimTypes.Role)?.Value
                ?? Context.User?.FindFirst("role")?.Value
                ?? Context.User?.FindFirst("Role")?.Value;
        }
    }
}
