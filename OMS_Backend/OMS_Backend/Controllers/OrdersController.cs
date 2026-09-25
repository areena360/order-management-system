using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OMS_Backend.DTOs;
using OMS_Backend.Services;
using System.Security.Claims;

namespace OMS_Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class OrdersController : ControllerBase
    {
        private readonly IOrderService _orderService;

        public OrdersController(IOrderService orderService)
        {
            _orderService = orderService;
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

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] OrderQueryDto query)
        {
            var result = await _orderService.GetOrdersAsync(query, CurrentUserId, IsCustomer());
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var order = await _orderService.GetOrderByIdAsync(id, CurrentUserId, IsCustomer());
            return Ok(order);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateOrderDto dto)
        {
            var order = await _orderService.CreateOrderAsync(dto, CurrentUserId, IsCustomer());
            return CreatedAtAction(nameof(GetById), new { id = order.Id }, order);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateOrderDto dto)
        {
            var order = await _orderService.UpdateOrderAsync(id, dto, CurrentUserId, IsCustomer());
            return Ok(order);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _orderService.DeleteOrderAsync(id, CurrentUserId, IsCustomer());
            return NoContent();
        }

        [HttpPatch("{id}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateOrderStatusDto dto)
        {
            var order = await _orderService.UpdateOrderStatusAsync(id, dto, CurrentUserId, IsCustomer());
            return Ok(order);
        }

        // ===== NEW: Assign orders (bulk) =====
        [HttpPost("assign")]
        public async Task<IActionResult> AssignOrders([FromBody] AssignOrdersDto dto)
        {
            var result = await _orderService.AssignOrdersAsync(dto, CurrentUserId, IsCustomer());
            return Ok(result);
        }

        [HttpPost("{id}/images")]
        public async Task<IActionResult> UploadImages(int id, [FromForm] List<IFormFile> files)
        {
            if (files == null || files.Count == 0)
                return BadRequest(new { message = "No files provided." });

            var images = await _orderService.AddOrderImagesAsync(id, files, CurrentUserId, IsCustomer());
            return Ok(images);
        }

        [HttpDelete("{id}/images/{imageId}")]
        public async Task<IActionResult> DeleteImage(int id, int imageId)
        {
            await _orderService.DeleteOrderImageAsync(id, imageId, CurrentUserId, IsCustomer());
            return NoContent();
        }

        [HttpGet("{id}/inventory-bill")]
        public async Task<IActionResult> GetInventoryBills(int id)
        {
            var bills = await _orderService.GetInventoryBillsAsync(id, CurrentUserId, IsCustomer());
            return Ok(bills);
        }

        [HttpPost("{id}/inventory-bill")]
        public async Task<IActionResult> AddInventoryBill(int id, [FromForm] SaveInventoryBillDto dto)
        {
            var bill = await _orderService.AddInventoryBillAsync(id, dto, CurrentUserId, IsCustomer());
            return Ok(bill);
        }

        [HttpDelete("{id}/inventory-bill/{billId}")]
        public async Task<IActionResult> DeleteInventoryBill(int id, int billId)
        {
            await _orderService.DeleteInventoryBillAsync(id, billId, CurrentUserId, IsCustomer());
            return NoContent();
        }

        [HttpPut("{id}/inventory-bill/{billId}")]
        public async Task<IActionResult> UpdateInventoryBill(int id, int billId, [FromForm] SaveInventoryBillDto dto)
        {
            return Ok(await _orderService.UpdateInventoryBillAsync(id, billId, dto, CurrentUserId, IsCustomer()));
        }
    }
}
