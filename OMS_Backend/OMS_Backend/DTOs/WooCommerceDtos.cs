using System.ComponentModel.DataAnnotations;

namespace OMS_Backend.DTOs;

public class WooStartDto
{
    public Guid InstallationId { get; set; }
    [Required, MaxLength(200)] public string StoreName { get; set; } = "";
    [Required, MaxLength(500)] public string StoreUrl { get; set; } = "";
    [Required, RegularExpression("^[a-f0-9]{64}$")] public string TokenHash { get; set; } = "";
    [Required, MaxLength(30)] public string PluginVersion { get; set; } = "";
}
public class WooApproveDto
{
    [Required, MaxLength(64)] public string Code { get; set; } = "";
    public int? OwnerUserId { get; set; }
    public int DefaultGenderId { get; set; }
    public int DefaultMaterialId { get; set; }
    public int DefaultStatusId { get; set; }
}
public class WooSettingsDto
{
    public int DefaultGenderId { get; set; }
    public int DefaultMaterialId { get; set; }
    public int DefaultStatusId { get; set; }
    public Dictionary<int, string> StatusMappings { get; set; } = new();
}
public class WooAddressDto
{
    [MaxLength(100)] public string FirstName { get; set; } = "";
    [MaxLength(100)] public string LastName { get; set; } = "";
    [MaxLength(200)] public string Company { get; set; } = "";
    [MaxLength(300)] public string Address1 { get; set; } = "";
    [MaxLength(300)] public string Address2 { get; set; } = "";
    [MaxLength(100)] public string City { get; set; } = "";
    [MaxLength(100)] public string State { get; set; } = "";
    [MaxLength(30)] public string Postcode { get; set; } = "";
    [MaxLength(3)] public string Country { get; set; } = "";
    [MaxLength(254)] public string Email { get; set; } = "";
    [MaxLength(60)] public string Phone { get; set; } = "";
}
public class WooLineDto
{
    [Range(1, long.MaxValue)] public long Id { get; set; }
    public long ProductId { get; set; }
    public long VariationId { get; set; }
    [Required, MaxLength(300)] public string Name { get; set; } = "";
    [MaxLength(200)] public string Sku { get; set; } = "";
    [Range(1, 1000000)] public int Quantity { get; set; }
    [Range(0, 999999999)] public decimal Total { get; set; }
    [MaxLength(2000)] public string ImageUrl { get; set; } = "";
    [MaxLength(100)] public List<string> ImageUrls { get; set; } = new();
    [MaxLength(4000)] public string Attributes { get; set; } = "";
}
public class WooOrderDto
{
    [Range(1, long.MaxValue)] public long Id { get; set; }
    [Required, MaxLength(100)] public string Number { get; set; } = "";
    public long CustomerId { get; set; }
    [Required, MaxLength(30)] public string Status { get; set; } = "";
    [Required, RegularExpression("^[A-Z]{3}$")] public string Currency { get; set; } = "";
    [Range(0, 999999999)] public decimal Total { get; set; }
    [MaxLength(100)] public string PaymentMethod { get; set; } = "";
    [MaxLength(4000)] public string CustomerNote { get; set; } = "";
    public DateTime ModifiedAt { get; set; }
    [Required] public WooAddressDto Billing { get; set; } = new();
    [Required] public WooAddressDto Shipping { get; set; } = new();
    [Required, MinLength(1), MaxLength(200)] public List<WooLineDto> Items { get; set; } = new();
}
