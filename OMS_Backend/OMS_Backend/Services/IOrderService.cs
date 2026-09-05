using OMS_Backend.DTOs;

namespace OMS_Backend.Services
{
    public interface IOrderService
    {
        Task<PagedResult<OrderListDto>> GetOrdersAsync(OrderQueryDto query);
        Task<OrderDetailsDto> GetOrderByIdAsync(int id);
        Task<OrderDetailsDto> CreateOrderAsync(CreateOrderDto dto, int userId);
        Task<OrderDetailsDto> UpdateOrderAsync(int id, UpdateOrderDto dto, int userId);
        Task<OrderDetailsDto> UpdateOrderStatusAsync(int id, UpdateOrderStatusDto dto, int userId);

        Task<List<OrderImageDto>> AddOrderImagesAsync(int orderId, List<IFormFile> files);
        Task DeleteOrderImageAsync(int orderId, int imageId);

        Task<List<InventoryBillDto>> GetInventoryBillsAsync(int orderId);
        Task<InventoryBillDto> AddInventoryBillAsync(int orderId, SaveInventoryBillDto dto);
    }
}
