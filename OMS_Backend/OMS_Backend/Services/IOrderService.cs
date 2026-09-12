using OMS_Backend.DTOs;

namespace OMS_Backend.Services
{
    public interface IOrderService
    {
        Task DeleteInventoryBillAsync(int orderId, int billId, int userId, bool isCustomer);
        Task<PagedResult<OrderListDto>> GetOrdersAsync(OrderQueryDto query, int userId, bool isCustomer);
        Task<OrderDetailsDto> GetOrderByIdAsync(int id, int userId, bool isCustomer);

        Task<OrderDetailsDto> CreateOrderAsync(CreateOrderDto dto, int userId, bool isCustomer);
        Task<OrderDetailsDto> UpdateOrderAsync(int id, UpdateOrderDto dto, int userId, bool isCustomer);
        Task<OrderDetailsDto> UpdateOrderStatusAsync(int id, UpdateOrderStatusDto dto, int userId, bool isCustomer);
        Task DeleteOrderAsync(int id, int userId, bool isCustomer);

        Task<List<OrderImageDto>> AddOrderImagesAsync(int orderId, List<IFormFile> files, int userId, bool isCustomer);
        Task DeleteOrderImageAsync(int orderId, int imageId, int userId, bool isCustomer);

        Task<List<InventoryBillDto>> GetInventoryBillsAsync(int orderId, int userId, bool isCustomer);
        Task<InventoryBillDto> AddInventoryBillAsync(int orderId, SaveInventoryBillDto dto, int userId, bool isCustomer);
    }
}