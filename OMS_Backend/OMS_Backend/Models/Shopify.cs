using System.ComponentModel.DataAnnotations;
namespace OMS_Backend.Models;

public class ShopifyStore {
    public int Id { get; set; }
    [MaxLength(255)] public string Shop { get; set; } = "";
    public int ConnectionId { get; set; }
    public WooCommerceConnection Connection { get; set; } = null!;
    public string ProtectedToken { get; set; } = "";
    public string ProtectedRefreshToken { get; set; } = "";
    public DateTime? TokenExpiresAt { get; set; }
    public string Scopes { get; set; } = "";
    public DateTime NextPollAt { get; set; } = DateTime.UtcNow;
    public DateTime ScanSince { get; set; } = DateTime.UtcNow.AddDays(-30);
    public DateTime? ScanUntil { get; set; }
    public string? ScanCursor { get; set; }
    public bool FulfillmentEnabled { get; set; }
    public int? ShippedStatusId { get; set; }
    [MaxLength(300)] public string LastError { get; set; } = "";
}
public class ShopifyAuthorization {
    [Key, MaxLength(64)] public string StateHash { get; set; } = "";
    [MaxLength(255)] public string Shop { get; set; } = "";
    public int OwnerUserId { get; set; }
    public int GenderId { get; set; }
    public int MaterialId { get; set; }
    public int StatusId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool Used { get; set; }
}
public class ShopifyJob {
    public long Id { get; set; }
    public int StoreId { get; set; }
    public ShopifyStore Store { get; set; } = null!;
    [MaxLength(150)] public string EventId { get; set; } = "";
    [MaxLength(50)] public string Topic { get; set; } = "orders/updated";
    public long ExternalId { get; set; }
    public int Attempts { get; set; }
    public DateTime DueAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    [MaxLength(300)] public string Error { get; set; } = "";
}
public class ShopifyShipment {
    public int Id { get; set; }
    public int StoreId { get; set; }
    public ShopifyStore Store { get; set; } = null!;
    public long ExternalOrderId { get; set; }
    public string FulfillmentIdsJson { get; set; } = "[]";
    public string TrackingHash { get; set; } = "";
}
