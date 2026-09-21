using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OMS_Backend.Data;
using OMS_Backend.DTOs;
using OMS_Backend.Models;
using OMS_Backend.Services;

// Run from repository root. Uses only persisted snapshots; never guesses retail data.
var config = new ConfigurationBuilder().SetBasePath(Path.GetFullPath("OMS_Backend/OMS_Backend")).AddJsonFile("appsettings.json").Build();
var options = new DbContextOptionsBuilder<OMSDbContext>().UseSqlServer(config.GetConnectionString("DefaultConnection")).Options;
await using var db = new OMSDbContext(options);
var legacy = await db.Set<WooCommerceOrder>().AsNoTracking().Include(x=>x.Order)
    .Where(x=>x.ExternalLineId==0 && x.Connection.IsActive && !x.Order.IsDeleted).ToListAsync();
Console.WriteLine($"Legacy WooCommerce orders to expand: {legacy.Count}");
foreach(var row in legacy) {
    var items = JsonSerializer.Deserialize<List<WooLineDto>>(row.ItemsJson)!;
    Console.WriteLine($"WooCommerce #{row.ExternalOrderId}: {items.Sum(x=>x.Quantity)} units; retain OMS #{row.OrderId}.");
    if(!args.Contains("--apply")) continue;
    await new WooCommerceIntegrationService(db).Import(row.ConnectionId,new WooOrderDto {
        Id=row.ExternalOrderId, Number=row.Order.CustomerOrderNumber ?? row.ExternalOrderId.ToString(),
        Status=row.ExternalStatus, Currency=row.Currency, Total=row.Total, ModifiedAt=row.ModifiedAt,
        PaymentMethod=row.PaymentMethod, CustomerNote=row.Order.NotesByCustomer ?? "",
        CustomerId=(await db.Set<WooCommerceCustomer>().FindAsync(row.BuyerId))!.ExternalCustomerId,
        Billing=JsonSerializer.Deserialize<WooAddressDto>(row.BillingJson)!, Shipping=JsonSerializer.Deserialize<WooAddressDto>(row.ShippingJson)!, Items=items
    });
    db.ChangeTracker.Clear();
}
Console.WriteLine(args.Contains("--apply") ? "Expansion complete; existing production rows retained." : "Dry run only. Pass --apply after verifying a database backup.");
