using System.ComponentModel.DataAnnotations;

namespace OMS_Backend.Models;

public class WooCommerceConnection
{
    [MaxLength(20)] public string Provider { get; set; } = "WooCommerce";
    public int Id { get; set; }
    public int OwnerUserId { get; set; }
    public User OwnerUser { get; set; } = null!;
    public Guid InstallationId { get; set; }
    [MaxLength(200)] public string StoreName { get; set; } = "";
    [MaxLength(500)] public string StoreUrl { get; set; } = "";
    [MaxLength(64)] public string AccessTokenHash { get; set; } = "";
    [MaxLength(30)] public string PluginVersion { get; set; } = "";
    public bool IsActive { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? LastSyncAt { get; set; }
    public int DefaultGenderId { get; set; }
    public int DefaultMaterialId { get; set; }
    public int DefaultStatusId { get; set; }
    public string StatusMappingsJson { get; set; } = "{}";
    public Guid? SyncRequestId { get; set; }
}

public class WooCommerceAuthorization
{
    public Guid Id { get; set; }
    [MaxLength(64)] public string CodeHash { get; set; } = "";
    [MaxLength(64)] public string TokenHash { get; set; } = "";
    public Guid InstallationId { get; set; }
    [MaxLength(200)] public string StoreName { get; set; } = "";
    [MaxLength(500)] public string StoreUrl { get; set; } = "";
    [MaxLength(30)] public string PluginVersion { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public int? ConnectionId { get; set; }
}

// Buyers are store-scoped contacts, never OMS login users.
public class WooCommerceCustomer
{
    public int Id { get; set; }
    public int ConnectionId { get; set; }
    public WooCommerceConnection Connection { get; set; } = null!;
    [MaxLength(100)] public string CustomerKey { get; set; } = "";
    public long ExternalCustomerId { get; set; }
    [MaxLength(254)] public string Email { get; set; } = "";
    [MaxLength(200)] public string Name { get; set; } = "";
    [MaxLength(60)] public string Phone { get; set; } = "";
}

public class WooCommerceOrder
{
    public int Id { get; set; }
    public int ConnectionId { get; set; }
    public WooCommerceConnection Connection { get; set; } = null!;
    public long ExternalOrderId { get; set; }
    public long ExternalLineId { get; set; }
    public int UnitNumber { get; set; } = 1;
    public bool IsCurrentUnit { get; set; } = true;
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public int BuyerId { get; set; }
    public WooCommerceCustomer Buyer { get; set; } = null!;
    [MaxLength(30)] public string ExternalStatus { get; set; } = "";
    [MaxLength(3)] public string Currency { get; set; } = "";
    public decimal Total { get; set; }
    public DateTime ModifiedAt { get; set; }
    [MaxLength(64)] public string PayloadHash { get; set; } = "";
    public string ItemsJson { get; set; } = "[]";
    public string BillingJson { get; set; } = "{}";
    public string ShippingJson { get; set; } = "{}";
    [MaxLength(100)] public string PaymentMethod { get; set; } = "";
}

public class WooCommerceSyncLog
{
    public long Id { get; set; }
    public int ConnectionId { get; set; }
    public WooCommerceConnection Connection { get; set; } = null!;
    public long? ExternalOrderId { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    [MaxLength(30)] public string Result { get; set; } = "";
    [MaxLength(300)] public string Message { get; set; } = "";
}
