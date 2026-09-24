using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OMS_Backend.Common.Exceptions;
using OMS_Backend.Data;
using OMS_Backend.DTOs;
using OMS_Backend.Models;

namespace OMS_Backend.Services;

public class WooCommerceIntegrationService(OMSDbContext db)
{
    public async Task ValidateDefaults(int gender, int material, int status)
    {
        if (!await db.LookupItems.Where(OrderStatusCatalog.Selectable).AnyAsync(x => x.Id == status))
            throw new ValidationAppException("Choose Assign, In Manufacturing, Refund or Cancel as the initial OMS status.");
        foreach (var (id, type) in new[] { (gender, 3), (material, 4), (status, 1) })
            if (!await db.LookupItems.AnyAsync(x => x.Id == id && x.LookupDataTypeId == type && x.IsActive && !x.IsDeleted))
                throw new ValidationAppException("Choose valid active gender, material and initial order status defaults.");
    }

    // A transaction-scoped SQL lock serializes imports and approval for each installation across API instances.
    public async Task Lock(string resource)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @result int; EXEC @result = sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @result < 0 THROW 51000, 'Integration busy. Retry request.', 1;");
    }

    public async Task<object> Import(int connectionId, WooOrderDto dto)
    {
        // This is the store's version timestamp, not a credential expiry. Independent
        // store/OMS clocks can differ; compare versions from the same store below.
        if (dto.ModifiedAt.Kind != DateTimeKind.Utc || dto.ModifiedAt < new DateTime(2000, 1, 1))
            throw new ValidationAppException("modifiedAt must be a valid UTC timestamp.");
        if (dto.Items.Select(x => x.Id).Distinct().Count() != dto.Items.Count)
            throw new ValidationAppException("Duplicate line item IDs.");
        if (dto.Items.Count == 0 || dto.Items.Any(x => x.Quantity < 1) || dto.Items.Sum(x => (long)x.Quantity) > 1000)
            throw new ValidationAppException("An order must contain between 1 and 1000 production units.");
        foreach (var item in dto.Items) {
            item.ImageUrls = new[] { item.ImageUrl }.Concat(item.ImageUrls ?? new())
                .Where(url => url != null && url.Length <= 2000 && Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                    uri.Scheme == "https" && string.IsNullOrEmpty(uri.UserInfo)).Distinct().Take(100).ToList();
            item.ImageUrl = item.ImageUrls.FirstOrDefault() ?? "";
        }

        using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await Lock($"woo:{connectionId}");
        var c = await db.Set<WooCommerceConnection>().SingleAsync(x => x.Id == connectionId);
        var prefix = c.Provider == "Shopify" ? "SH" : "WC";
        if (!c.IsActive) throw new UnauthorizedAppException();
        var hash = WooCommerceSecurity.Hash(JsonSerializer.Serialize(dto));
        var links = await db.Set<WooCommerceOrder>().Include(x => x.Order)
            .Where(x => x.ConnectionId == connectionId && x.ExternalOrderId == dto.Id).OrderBy(x => x.Id).ToListAsync();
        var legacy = links.SingleOrDefault(x => x.ExternalLineId == 0);
        if (links.Count > 0 && (dto.ModifiedAt < links.Max(x => x.ModifiedAt) ||
            (legacy == null && links.All(x => hash == x.PayloadHash))))
        {
            c.LastSyncAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return new { orderId = links[0].OrderId, orderIds = links.Select(x => x.OrderId).ToArray(), result = "unchanged" };
        }

        var email = dto.Billing.Email.Trim().ToLowerInvariant();
        var key = dto.CustomerId > 0 ? $"id:{dto.CustomerId}" : email.Length > 0 ? $"email:{WooCommerceSecurity.Hash(email)}" : $"guest-order:{dto.Id}";
        var buyer = await db.Set<WooCommerceCustomer>().SingleOrDefaultAsync(x => x.ConnectionId == connectionId && x.CustomerKey == key);
        if (buyer == null)
        {
            buyer = new WooCommerceCustomer { ConnectionId = connectionId, CustomerKey = key };
            db.Add(buyer);
        }
        buyer.ExternalCustomerId = dto.CustomerId;
        buyer.Email = email;
        buyer.Name = $"{dto.Billing.FirstName} {dto.Billing.LastName}".Trim();
        if (buyer.Name.Length > 200) buyer.Name = buyer.Name[..200];
        buyer.Phone = dto.Billing.Phone;
        var created = links.Count == 0;
        var added = 0;
        // Upgrade the old single row in place; retain its ID, number and production edits.
        if (legacy != null) {
            legacy.ExternalLineId = dto.Items[0].Id; legacy.UnitNumber = 1;
            var oldAutoTitle = string.Join("; ", dto.Items.Select(x => $"{x.Name} × {x.Quantity}"));
            if (legacy.Order.CustomerProductTitle == oldAutoTitle) legacy.Order.CustomerProductTitle = dto.Items[0].Name;
        }
        foreach (var old in links) old.IsCurrentUnit = false;
        foreach (var item in dto.Items)
        for (var unit = 1; unit <= item.Quantity; unit++)
        {
            var link = links.SingleOrDefault(x => x.ExternalLineId == item.Id && x.UnitNumber == unit);
            if (link == null)
            {
            await ValidateDefaults(c.DefaultGenderId, c.DefaultMaterialId, c.DefaultStatusId);
            var address = string.IsNullOrWhiteSpace(dto.Shipping.Address1) ? dto.Billing : dto.Shipping;
            var order = new Order {
                CustomerId = c.OwnerUserId,
                CustomerOrderNumber = dto.Number,
                // Separate namespace avoids interfering with the existing AD sequence.
                ManufacturerOrderNumber = links.Count == 0 ? $"{prefix}{connectionId}-{dto.Id}" : $"{prefix}{connectionId}-{dto.Id}-{item.Id}-{unit}",
                CustomerProductTitle = item.Name,
                // Existing integer Amount represents OMS manufacturing pricing, not the retail total.
                Amount = null,
                GenderId = c.DefaultGenderId, CustomerMaterialId = c.DefaultMaterialId,
                ManufacturerMaterialId = c.DefaultMaterialId, OrderStatusId = c.DefaultStatusId,
                IsCustomSize = true, SizeDetails = $"See {c.Provider} line item attributes; confirm production sizing.",
                ConsigneeName = $"{address.FirstName} {address.LastName}".Trim(),
                ConsigneeAddress = string.Join(", ", new[] { address.Company, address.Address1, address.Address2, address.City, address.State, address.Postcode, address.Country }.Where(x => !string.IsNullOrWhiteSpace(x))),
                NotesByCustomer = dto.CustomerNote, IsActive = true, CreatedBy = c.OwnerUserId,
                RequiresCustomerAssignment = true, DaysForMaking = 0,
                OrderImages = new List<OrderImage>(), InventoryBills = new List<InventoryBill>(),
                StatusHistories = new List<OrderStatusHistory> {
                    new() { StatusId = c.DefaultStatusId, IsActive = true, CreatedBy = c.OwnerUserId, CreatedDate = DateTime.UtcNow }
                }
            };
            link = new WooCommerceOrder { ConnectionId = connectionId, ExternalOrderId = dto.Id, ExternalLineId = item.Id, UnitNumber = unit, Order = order, Buyer = buyer };
            db.Add(link);
            links.Add(link);
            added++;
            }
            link.IsCurrentUnit = true;
        }
        // Preserve production edits. Incoming updates refresh the store snapshot, never overwrite OMS work.
        foreach (var link in links)
        {
        link.Buyer = buyer;
        link.ExternalStatus = dto.Status;
        link.Currency = dto.Currency;
        link.Total = dto.Total;
        link.ModifiedAt = dto.ModifiedAt;
        link.PayloadHash = hash;
        link.ItemsJson = JsonSerializer.Serialize(dto.Items);
        link.BillingJson = JsonSerializer.Serialize(dto.Billing);
        link.ShippingJson = JsonSerializer.Serialize(dto.Shipping);
        link.PaymentMethod = dto.PaymentMethod;
        }
        c.LastSyncAt = DateTime.UtcNow;
        db.Add(new WooCommerceSyncLog { ConnectionId = connectionId, ExternalOrderId = dto.Id,
            Result = created ? "imported" : "updated", Message = $"{added} production rows added; {links.Count(x => !x.IsCurrentUnit)} retained rows need quantity review. Existing production edits retained." });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return new { orderId = links[0].OrderId, orderIds = links.Select(x => x.OrderId).ToArray(), result = created ? "imported" : "updated" };
    }
}
