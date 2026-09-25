using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Configuration;
using OMS_Backend.Data;
using OMS_Backend.Migrations;
using OMS_Backend.Services;

// Run from repository root. Test all data changes inside a rolled-back transaction.
// --apply then applies ONLY this migration to the configured local development DB.
const string migrationId = "20260924100000_RestrictOrderStatuses";
var config = new ConfigurationBuilder().SetBasePath(Path.GetFullPath("OMS_Backend/OMS_Backend"))
    .AddJsonFile("appsettings.json").AddJsonFile("appsettings.Development.json", true)
    .AddJsonFile("appsettings.Local.json", true).AddEnvironmentVariables().Build();
var connection = new SqlConnectionStringBuilder(config.GetConnectionString("DefaultConnection"));
var host = connection.DataSource.Split('\\')[0].Split(',')[0];
if (!(host.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase)
      || new[] { ".", "localhost", "127.0.0.1", "(localdb)" }.Contains(host.ToLowerInvariant())))
    throw new InvalidOperationException("This smoke test is restricted to a local development SQL Server.");
await using var db = new OMSDbContext(new DbContextOptionsBuilder<OMSDbContext>().UseSqlServer(connection.ConnectionString).Options);
if (args.Contains("--forms"))
{
    await FormChecks.Run(db, config);
    return;
}
if (args.Contains("--assignment"))
{
    await AssignmentChecks.Run(db, config, args.Contains("--apply"));
    return;
}
if ((await db.Database.GetAppliedMigrationsAsync()).Contains(migrationId))
{
    var choices = await db.LookupItems.Where(OrderStatusCatalog.Selectable).Select(x => x.Name).ToListAsync();
    Check(choices.Order().SequenceEqual(OrderStatusCatalog.Names.Order()), "Applied status catalog still has exactly four choices");
    return;
}
var pending = (await db.Database.GetPendingMigrationsAsync()).ToArray();
if (pending.Any(id => id != migrationId))
    throw new InvalidOperationException("Apply earlier schema migrations before running this test.");

var oldHistoryCount = await db.OrderStatusHistories.CountAsync();
var oldStatuses = await db.LookupItems.Where(x => x.LookupDataTypeId == 1).ToListAsync();
var assignId = oldStatuses.Where(x => x.Name.Trim().Equals("Assign", StringComparison.OrdinalIgnoreCase))
    .Select(x => (int?)x.Id).Min();
var changedOrders = await db.Orders.CountAsync(x => !assignId.HasValue || x.OrderStatusId != assignId);
await using (var tx = await db.Database.BeginTransactionAsync())
{
    foreach (var operation in new RestrictOrderStatuses().UpOperations.Cast<SqlOperation>())
        await db.Database.ExecuteSqlRawAsync(operation.Sql);
    var active = await db.LookupItems.AsNoTracking().Where(OrderStatusCatalog.Selectable).ToListAsync();
    Check(active.Count == 4 && active.Select(x => x.Name).Order().SequenceEqual(OrderStatusCatalog.Names.Order()), "Exactly four canonical choices");
    Check(await db.LookupItems.CountAsync(x => x.LookupDataTypeId == 1 && x.IsActive && !x.IsDeleted) == 4, "No old status remains selectable");
    var newAssignId = active.Single(x => x.Name == OrderStatusCatalog.Assign).Id;
    Check(!await db.Orders.AnyAsync(x => x.OrderStatusId != newAssignId), "All existing orders reset to Assign");
    Check(await db.OrderStatusHistories.CountAsync() == oldHistoryCount + changedOrders, "Prior history preserved and reset recorded");
    var canonicalIds = active.Select(x => x.Id).ToArray();
    Check(!await db.Set<OMS_Backend.Models.WooCommerceConnection>().AnyAsync(x => !canonicalIds.Contains(x.DefaultStatusId)), "Integration defaults remain valid");
    Check(!await db.Set<OMS_Backend.Models.ShopifyStore>().AnyAsync(x => x.FulfillmentEnabled && x.ShippedStatusId.HasValue && !canonicalIds.Contains(x.ShippedStatusId.Value)), "Retired fulfillment triggers disabled");
    var controller = new OMS_Backend.Controllers.LookupsController(db);
    var response = (Microsoft.AspNetCore.Mvc.OkObjectResult)await controller.GetByType(1);
    var options = (List<OMS_Backend.DTOs.LookupItemDto>)response.Value!;
    Check(options.Select(x => x.Name).SequenceEqual(OrderStatusCatalog.Names), "Shared API returns the requested dropdown order");
    var retired = oldStatuses.FirstOrDefault(x => !OrderStatusCatalog.Names.Contains(x.Name));
    var orderId = await db.Orders.Where(x => !x.IsDeleted).Select(x => (int?)x.Id).FirstOrDefaultAsync();
    if (retired != null && orderId.HasValue)
    {
        var service = new OrderService(db, null!, config);
        try
        {
            await service.UpdateOrderStatusAsync(orderId.Value, new OMS_Backend.DTOs.UpdateOrderStatusDto { StatusId = retired.Id }, 0, false);
            throw new Exception("A retired status was accepted by the backend.");
        }
        catch (OMS_Backend.Common.Exceptions.ValidationAppException)
        {
            Console.WriteLine("PASS: backend rejects retired status IDs");
        }
    }
    await tx.RollbackAsync();
}
db.ChangeTracker.Clear();
Check(await db.OrderStatusHistories.CountAsync() == oldHistoryCount, "Dry-run changes rolled back");
if (args.Contains("--apply"))
{
    await db.Database.MigrateAsync();
    Check(!(await db.Database.GetPendingMigrationsAsync()).Any(), "Migration recorded as applied");
    var persisted = await db.LookupItems.AsNoTracking().Where(OrderStatusCatalog.Selectable).ToListAsync();
    Check(persisted.Count == 4, "Four selectable statuses persisted");
    Console.WriteLine("APPLIED: four-status catalog and existing-order reset to local development database.");
}
else Console.WriteLine("PASS: migration dry run; database unchanged. Use --apply to apply the verified migration.");

static void Check(bool result, string message)
{
    if (!result) throw new Exception(message);
    Console.WriteLine("PASS: " + message);
}
