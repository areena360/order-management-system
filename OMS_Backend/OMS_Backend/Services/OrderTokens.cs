using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using OMS_Backend.Data;

namespace OMS_Backend.Services;

// Short-lived, purpose-bound grants. Every use still checks current database permissions.
public sealed class OrderTokens(IDataProtectionProvider protection)
{
    private readonly IDataProtector files = protection.CreateProtector("OMS.OrderFiles.v1");
    private readonly IDataProtector creation = protection.CreateProtector("OMS.OrderCreationAttachments.v1");
    private sealed record Grant(int UserId, string Resource, DateTimeOffset Expires);

    private static string Issue(IDataProtector protector, int userId, string resource) =>
        protector.Protect(JsonSerializer.Serialize(new Grant(userId, resource, DateTimeOffset.UtcNow.AddMinutes(15))));

    private static int? Read(IDataProtector protector, string? token, string resource)
    {
        if (string.IsNullOrEmpty(token)) return null;
        try
        {
            var grant = JsonSerializer.Deserialize<Grant>(protector.Unprotect(token));
            return grant != null && grant.Expires > DateTimeOffset.UtcNow && grant.Resource == resource ? grant.UserId : null;
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or FormatException) { return null; }
    }

    public string IssueCreation(int userId, int orderId) => Issue(creation, userId, orderId.ToString());
    public bool AllowsCreation(string? token, int userId, int orderId) => Read(creation, token, orderId.ToString()) == userId;
    public string? FileUrl(string? url, int userId)
    {
        if (url == null || !(url.StartsWith("/uploads/orders/", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("/uploads/inventory-bills/", StringComparison.OrdinalIgnoreCase))) return url;
        var plain = url.Split('?')[0];
        return plain + "?grant=" + Uri.EscapeDataString(Issue(files, userId, Uri.UnescapeDataString(plain)));
    }

    public async Task<bool> CanDownloadAsync(OMSDbContext db, string requestPath, string? token)
    {
        var canonical = Uri.UnescapeDataString(requestPath);
        var userId = Read(files, token, canonical);
        var parts = canonical.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (!userId.HasValue || parts.Length != 4 || !int.TryParse(parts[2], out var orderId)) return false;
        var access = await OrderAccess.LoadAsync(db, userId.Value);
        if (!access.CanView || !await db.Orders.Where(OrderVisibility.ForUser(db, userId.Value, access.IsCustomer))
                .AnyAsync(o => o.Id == orderId)) return false;
        if (parts[1].Equals("inventory-bills", StringComparison.OrdinalIgnoreCase))
        {
            if (!access.CanSeeSensitiveData) return false;
            var paths = await db.InventoryBills.Where(b => b.OrderId == orderId && !b.IsDeleted && b.BillImage != null)
                .Select(b => b.BillImage!).ToListAsync();
            return paths.Any(p => Uri.UnescapeDataString(p) == canonical);
        }
        if (!parts[1].Equals("orders", StringComparison.OrdinalIgnoreCase)) return false;
        var images = await db.OrderImages.Where(i => i.OrderId == orderId && !i.IsDeleted).Select(i => i.ImageURL).ToListAsync();
        return images.Any(p => Uri.UnescapeDataString(p) == canonical);
    }
}
