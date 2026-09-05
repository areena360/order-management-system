using System.ComponentModel.DataAnnotations;

namespace OMS_Backend.DTOs
{
    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
    }

    public class OrderQueryDto
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 25;
        public string? Search { get; set; }
        public string? SortBy { get; set; } = "CreatedDate";
        public string? SortDirection { get; set; } = "desc";

        public int? StatusId { get; set; }
        public int? PriorityId { get; set; }
        public int? CustomerId { get; set; }
        public int? GenderId { get; set; }
        public int? MaterialId { get; set; } // matches Customer or Manufacturer material
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
    }

    public class OrderListDto
    {
        public int Id { get; set; }
        public string ManufacturerOrderNumber { get; set; } = default!;
        public string? CustomerOrderNumber { get; set; }
        public string CustomerName { get; set; } = default!;
        public string CustomerProductTitle { get; set; } = default!;
        public string? ManufacturerProductTitle { get; set; }
        public int? Amount { get; set; }
        public string Status { get; set; } = default!;
        public int OrderStatusId { get; set; }
        public string? Priority { get; set; }
        public int DaysForMaking { get; set; }
        public string? TrackingNumber { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class OrderDetailsDto
    {
        public int Id { get; set; }
        public string ManufacturerOrderNumber { get; set; } = default!;
        public string? CustomerOrderNumber { get; set; }

        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = default!;

        public string CustomerProductTitle { get; set; } = default!;
        public string? ManufacturerProductTitle { get; set; }
        public int GenderId { get; set; }
        public string? Gender { get; set; }
        public int CustomerMaterialId { get; set; }
        public string? CustomerMaterial { get; set; }
        public int ManufacturerMaterialId { get; set; }
        public string? ManufacturerMaterial { get; set; }
        public int? Amount { get; set; }
        public int? PriorityId { get; set; }
        public string? Priority { get; set; }

        public bool IsCustomSize { get; set; }
        public int? SizeId { get; set; }
        public string? Size { get; set; }
        public int? SizeChartId { get; set; }
        public string? SizeChart { get; set; }
        public string? SizeDetails { get; set; }

        public int DaysForMaking { get; set; }

        public string ConsigneeName { get; set; } = default!;
        public string ConsigneeAddress { get; set; } = default!;
        public string? TrackingNumber { get; set; }

        public string? NotesByCustomer { get; set; }
        public string? NotesByManufacturer { get; set; }

        public int OrderStatusId { get; set; }
        public string Status { get; set; } = default!;

        public DateTime CreatedDate { get; set; }
        public DateTime? UpdatedDate { get; set; }

        public List<OrderImageDto> Images { get; set; } = new();
        public List<OrderStatusHistoryDto> StatusHistory { get; set; } = new();
        public List<InventoryBillDto> InventoryBills { get; set; } = new();
    }

    public class CreateOrderDto
    {
        [Required] public string CustomerProductTitle { get; set; } = default!;
        public string? ManufacturerProductTitle { get; set; }
        public string? CustomerOrderNumber { get; set; }

        [Required] public int CustomerId { get; set; }
        public int? Amount { get; set; }

        [Required] public int GenderId { get; set; }
        [Required] public int CustomerMaterialId { get; set; }
        [Required] public int ManufacturerMaterialId { get; set; }

        public bool IsCustomSize { get; set; }
        public int? SizeId { get; set; }
        public int? SizeChartId { get; set; }
        public string? SizeDetails { get; set; }

        [Required, Range(0, 3650)] public int DaysForMaking { get; set; }
        public int? PriorityId { get; set; }

        [Required] public string ConsigneeName { get; set; } = default!;
        [Required] public string ConsigneeAddress { get; set; } = default!;
        public string? TrackingNumber { get; set; }

        public string? NotesByCustomer { get; set; }
        public string? NotesByManufacturer { get; set; }
    }

    public class UpdateOrderDto
    {
        [Required] public string CustomerProductTitle { get; set; } = default!;
        public string? ManufacturerProductTitle { get; set; }
        public string? CustomerOrderNumber { get; set; }

        [Required] public int CustomerId { get; set; }
        public int? Amount { get; set; }

        [Required] public int GenderId { get; set; }
        [Required] public int CustomerMaterialId { get; set; }
        [Required] public int ManufacturerMaterialId { get; set; }

        public bool IsCustomSize { get; set; }
        public int? SizeId { get; set; }
        public int? SizeChartId { get; set; }
        public string? SizeDetails { get; set; }

        [Required, Range(0, 3650)] public int DaysForMaking { get; set; }
        public int? PriorityId { get; set; }

        [Required] public string ConsigneeName { get; set; } = default!;
        [Required] public string ConsigneeAddress { get; set; } = default!;
        public string? TrackingNumber { get; set; }

        public string? NotesByCustomer { get; set; }
        public string? NotesByManufacturer { get; set; }
    }

    public class UpdateOrderStatusDto
    {
        [Required] public int StatusId { get; set; }
    }

    public class OrderImageDto
    {
        public int Id { get; set; }
        public string ImageURL { get; set; } = default!;
    }

    public class OrderStatusHistoryDto
    {
        public int Id { get; set; }
        public int StatusId { get; set; }
        public string Status { get; set; } = default!;
        public DateTime CreatedDate { get; set; }
        public string? ChangedByName { get; set; }
    }

    public class InventoryBillDto
    {
        public int Id { get; set; }
        public int? BillNumber { get; set; }
        public string BillDetails { get; set; } = default!;
        public string? BillImage { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class SaveInventoryBillDto
    {
        public int? BillNumber { get; set; }
        [Required] public string BillDetails { get; set; } = default!;
        public string? BillImage { get; set; }
    }

    public class LookupItemDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = default!;
    }
}
