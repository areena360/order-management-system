using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OMS_Backend.Services;
using System.Security.Claims;

namespace OMS_Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ChatController : ControllerBase
    {
        private readonly IChatService _chatService;

        public ChatController(IChatService chatService)
        {
            _chatService = chatService;
        }

        private int CurrentUserId =>
            int.Parse(User.FindFirst("userId")?.Value ?? "0");

        private bool IsCustomer()
        {
            var roleClaim =
                User.FindFirst(ClaimTypes.Role)?.Value
                ?? User.FindFirst("role")?.Value
                ?? User.FindFirst("Role")?.Value;

            return string.Equals(roleClaim, "Customer", StringComparison.OrdinalIgnoreCase);
        }

        [HttpGet("order/{orderId}")]
        public async Task<IActionResult> GetOrderConversation(int orderId)
        {
            var result = await _chatService.GetConversationAsync(
                orderId, CurrentUserId, IsCustomer());

            return Ok(result);
        }
    }
}