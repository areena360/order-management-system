using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OMS_Backend.DTOs;
using OMS_Backend.Services;

namespace OMS_Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class OrdersController : ControllerBase
    {
        private readonly IOrderService _orderService;
        public OrdersController(IOrderService orderService) => _orderService = orderService;

        private int CurrentUserId =>
            int.Parse(User.FindFirst("userId")?.Value ?? "0");

        // GET api/orders?pageNumber=&pageSize=&search=&sortBy=&sortDirection=&statusId=&priorityId=...
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] OrderQueryDto query)
        {
            var result = await _orderService.GetOrdersAsync(query);
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var order = await _orderService.GetOrderByIdAsync(id);
            return Ok(order);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateOrderDto dto)
        {
            var order = await _orderService.CreateOrderAsync(dto, CurrentUserId);
            return CreatedAtAction(nameof(GetById), new { id = order.Id }, order);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateOrderDto dto)
        {
            var order = await _orderService.UpdateOrderAsync(id, dto, CurrentUserId);
            return Ok(order);
        }

        [HttpPatch("{id}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateOrderStatusDto dto)
        {
            var order = await _orderService.UpdateOrderStatusAsync(id, dto, CurrentUserId);
            return Ok(order);
        }

        [HttpPost("{id}/images")]
        public async Task<IActionResult> UploadImages(int id, [FromForm] List<IFormFile> files)
        {
            if (files == null || files.Count == 0)
                return BadRequest(new { message = "No files provided." });

            var images = await _orderService.AddOrderImagesAsync(id, files);
            return Ok(images);
        }

        [HttpDelete("{id}/images/{imageId}")]
        public async Task<IActionResult> DeleteImage(int id, int imageId)
        {
            await _orderService.DeleteOrderImageAsync(id, imageId);
            return NoContent();
        }

        [HttpGet("{id}/inventory-bill")]
        public async Task<IActionResult> GetInventoryBills(int id)
        {
            var bills = await _orderService.GetInventoryBillsAsync(id);
            return Ok(bills);
        }

        [HttpPost("{id}/inventory-bill")]
        public async Task<IActionResult> AddInventoryBill(int id, [FromBody] SaveInventoryBillDto dto)
        {
            var bill = await _orderService.AddInventoryBillAsync(id, dto);
            return Ok(bill);
        }
    }
}
