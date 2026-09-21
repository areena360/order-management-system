using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OMS_Backend.Common.Exceptions;
using OMS_Backend.Controllers;
using OMS_Backend.Data;
using OMS_Backend.DTOs;
using OMS_Backend.Models;
using OMS_Backend.Services;

if (args.Contains("--prepare-local")) { await LocalDemo.Prepare(); return; }

// Deliberately never reads the application's connection string or touches its database.
var database = "OMS_Woo_Test_" + Guid.NewGuid().ToString("N");
var connectionString = $"Server=(localdb)\\MSSQLLocalDB;Database={database};Integrated Security=true;TrustServerCertificate=true";
var options = new DbContextOptionsBuilder<OMSDbContext>().UseSqlServer(connectionString).Options;
var checks = 0;
void Assert(bool condition, string name) { if (!condition) throw new Exception("FAIL: " + name); checks++; Console.WriteLine("PASS: " + name); }
async Task Denied<T>(Func<Task> action, string name) where T : Exception {
    try { await action(); } catch (T) { Assert(true, name); return; }
    throw new Exception("FAIL: " + name);
}
JsonElement Json(IActionResult result) => JsonSerializer.SerializeToElement(((ObjectResult)result).Value);
WooCommerceIntegrationController Controller(OMSDbContext db, int userId, string role = "Customer", int? connection = null) {
    var claims = new List<Claim> {new("userId", userId.ToString()), new(ClaimTypes.Role, role)};
    if (connection.HasValue) claims.Add(new("connectionId", connection.Value.ToString()));
    return new(db, new(db)) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext {
        User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
    } } };
}
WooOrderDto Payload(long id = 1532) => new() {
    Id = id, Number = id.ToString(), CustomerId = 87, Status = "processing", Currency = "USD", Total = 123.45m,
    ModifiedAt = DateTime.UtcNow.AddMinutes(-1), Billing = new() { FirstName = "Buyer", Email = "buyer@example.test", Address1 = "Test address", Country = "PK" },
    Items = new() { new() { Id = 1, Name = "Shirt", ProductId = 456, Sku = "SHIRT-001", Quantity = 1, Total = 123.45m } }
};
var localConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {{"WooCommerce:AllowLocalHttp", "true"}}).Build();
var localEnvironment = new Microsoft.Extensions.Hosting.Internal.HostingEnvironment {EnvironmentName = "Development"};
var transport = new DefaultHttpContext(); transport.Request.Scheme = "http"; transport.Request.Host = new HostString("localhost",5511);
transport.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;
Assert(WooCommerceSecurity.TransportAllowed(transport.Request, localConfig, localEnvironment), "explicit development loopback HTTP allowed");
localEnvironment.EnvironmentName = "Production";
Assert(!WooCommerceSecurity.TransportAllowed(transport.Request, localConfig, localEnvironment), "production rejects HTTP even with local option enabled");
localEnvironment.EnvironmentName = "Development"; transport.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.0.2.1");
Assert(!WooCommerceSecurity.TransportAllowed(transport.Request, localConfig, localEnvironment), "non-loopback client rejected in local HTTP mode");
transport.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback; transport.Request.Host = new HostString("untrusted.example");
Assert(!WooCommerceSecurity.TransportAllowed(transport.Request, localConfig, localEnvironment), "non-loopback Host rejected in local HTTP mode");
await using var db = new OMSDbContext(options);
try {
    await MigrationSafety.Verify(options);
    Assert(true, "additive migration preserves pre-existing order data on real SQL Server");
    var owner = new User { FirstName = "Store", LastName = "A", Email = "a@example.test", FirstContact = "0", Password = "not-a-login", RoleId = 4, IsActive = true };
    var other = new User { FirstName = "Store", LastName = "B", Email = "b@example.test", FirstContact = "0", Password = "not-a-login", RoleId = 4, IsActive = true };
    var status = new LookupItem { LookupDataTypeId = 1, Name = "New", IsActive = true };
    db.AddRange(owner, other, status); await db.SaveChangesAsync();
    var token = WooCommerceSecurity.Secret(); var installation = Guid.NewGuid();
    var controller = Controller(db, owner.Id);
    var start = Json(await controller.Start(new() { InstallationId = installation, StoreName = "Store A", StoreUrl = "https://a.example.test", TokenHash = WooCommerceSecurity.Hash(token), PluginVersion = "1.0.0" }));
    var device = start.GetProperty("deviceId").GetGuid(); var code = start.GetProperty("code").GetString()!;
    var pendingController = Controller(db, 0); pendingController.Request.Headers["X-OMS-Device-Secret"] = token;
    Assert(Json(await pendingController.Poll(device)).GetProperty("state").GetString() == "pending", "device pending before owner approval");
    var approved = Json(await controller.Approve(new() { Code = code, OwnerUserId = other.Id, DefaultGenderId = 5, DefaultMaterialId = 12, DefaultStatusId = status.Id }));
    var id = approved.GetProperty("connectionId").GetInt32();
    Assert((await db.Set<WooCommerceConnection>().FindAsync(id))!.OwnerUserId == owner.Id, "customer cannot choose another store owner");
    Assert(Json(await pendingController.Poll(device)).GetProperty("connectionId").GetInt32() == id, "approved device receives bound connection");
    pendingController.Request.Headers["X-OMS-Device-Secret"] = "wrong";
    Assert(await pendingController.Poll(device) is UnauthorizedResult, "incorrect device secret denied");
    await Denied<ValidationAppException>(async () => { await controller.Approve(new() {Code = code, DefaultGenderId = 5, DefaultMaterialId = 12, DefaultStatusId = status.Id}); }, "approval code cannot be replayed");
    await Denied<NotFoundException>(async () => { await Controller(db, other.Id).Logs(id); }, "other customer cannot read store logs");
    await Denied<NotFoundException>(async () => { await Controller(db, other.Id).Disconnect(id); }, "other customer cannot revoke connection");
    await Denied<ForbiddenAppException>(async () => { await Controller(db, owner.Id, "Staff").Connections(); }, "staff cannot manage integrations");

    var service = new WooCommerceIntegrationService(db); var payload = Payload();
    await service.Import(id, payload); await service.Import(id, payload);
    Assert(await db.Orders.CountAsync() == 1 && await db.Set<WooCommerceOrder>().CountAsync() == 1, "duplicate order imports exactly once");
    var order = await db.Orders.SingleAsync();
    Assert(order.CustomerId == owner.Id && order.ManufacturerOrderNumber == $"WC{id}-1532", "import uses store owner and independent order number");
    Assert(order.Amount == null && (await db.Set<WooCommerceOrder>().SingleAsync()).Total == 123.45m, "decimal retail amount retained separately");
    Assert(await db.Users.CountAsync() == 2 && await db.Set<WooCommerceCustomer>().CountAsync() == 1, "buyer does not become an OMS login");
    order.CustomerProductTitle = "Production edit"; order.TrackingNumber = "TRACK123"; await db.SaveChangesAsync();
    payload.Status = "cancelled"; payload.ModifiedAt = DateTime.UtcNow.AddHours(14);
    await service.Import(id, payload);
    Assert(order.CustomerProductTitle == "Production edit" && order.TrackingNumber == "TRACK123", "incoming cancellation preserves production edits");
    Assert((await db.Set<WooCommerceOrder>().SingleAsync()).ExternalStatus == "cancelled", "cancellation retained in retail snapshot");
    Assert((await db.Set<WooCommerceOrder>().SingleAsync()).ModifiedAt == payload.ModifiedAt, "store clock ahead of OMS retains its version timestamp");
    payload.Status = "processing"; payload.ModifiedAt = DateTime.UtcNow.AddHours(-1); await service.Import(id, payload);
    Assert((await db.Set<WooCommerceOrder>().SingleAsync()).ExternalStatus == "cancelled", "stale event cannot roll back snapshot");
    var c = (await db.Set<WooCommerceConnection>().FindAsync(id))!;
    c.StatusMappingsJson = JsonSerializer.Serialize(new Dictionary<int,string> {{status.Id, "completed"}}); await db.SaveChangesAsync();
    var updates = Json(await Controller(db, 0, "", id).Updates());
    Assert(updates.GetProperty("items")[0].GetProperty("status").GetString() == "completed" && updates.GetProperty("items")[0].GetProperty("TrackingNumber").GetString() == "TRACK123", "outbound status and tracking snapshot");
    await Denied<NotFoundException>(async () => { await Controller(db, other.Id).OrderSnapshot(order.Id); }, "buyer snapshot isolated by store owner");

    await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ => {
        await using var concurrentDb = new OMSDbContext(options);
        await new WooCommerceIntegrationService(concurrentDb).Import(id, Payload(2000));
    }));
    Assert(await db.Set<WooCommerceOrder>().CountAsync(x => x.ExternalOrderId == 2000) == 1, "eight concurrent deliveries create one order");
    for (var i = 1; i <= 100; i++) await service.Import(id, Payload(3000 + i));
    Assert(await db.Set<WooCommerceOrder>().CountAsync() == 102, "100-order import retains every order");
    Assert(await db.Set<WooCommerceCustomer>().CountAsync() == 1, "repeated external buyer maps within one store");
    var c2 = new WooCommerceConnection { OwnerUserId = other.Id, InstallationId = Guid.NewGuid(), StoreUrl = "https://b.example.test", StoreName = "B", IsActive = true, DefaultGenderId = 5, DefaultMaterialId = 12, DefaultStatusId = status.Id, AccessTokenHash = WooCommerceSecurity.Hash(WooCommerceSecurity.Secret()) };
    db.Add(c2); await db.SaveChangesAsync(); await service.Import(c2.Id, Payload());
    Assert(await db.Set<WooCommerceOrder>().CountAsync(x => x.ExternalOrderId == 1532) == 2, "same external order ID in two stores does not collide");
    Assert(await db.Set<WooCommerceCustomer>().CountAsync() == 2, "identical buyer email in two stores stays isolated");
    order.IsDeleted = true; await db.SaveChangesAsync(); payload.ModifiedAt = DateTime.UtcNow; await service.Import(id, payload);
    Assert(order.IsDeleted, "soft-deleted OMS order is not recreated");

    var services = new ServiceCollection(); services.AddLogging(); services.AddOptions();
    services.AddSingleton<IHostEnvironment>(new Microsoft.Extensions.Hosting.Internal.HostingEnvironment { EnvironmentName = "Production" });
    services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {{"WooCommerce:Enabled", "true"}}).Build());
    services.AddDbContext<OMSDbContext>(o => o.UseSqlServer(connectionString));
    services.AddAuthentication(WooCommerceSecurity.Scheme).AddScheme<AuthenticationSchemeOptions, WooCommerceAuthenticationHandler>(WooCommerceSecurity.Scheme, _ => {});
    await using var provider = services.BuildServiceProvider();
    async Task<bool> Authenticate(string credential) {
        using var scope = provider.CreateScope(); var context = new DefaultHttpContext {RequestServices = scope.ServiceProvider};
        context.Request.Scheme = "https"; context.Request.Headers.Authorization = "Bearer " + credential;
        context.Request.Headers["X-OMS-Connection"] = id.ToString();
        return (await context.AuthenticateAsync(WooCommerceSecurity.Scheme)).Succeeded;
    }
    Assert(await Authenticate(token), "active credential authenticates through real handler");
    Assert(!await Authenticate(new string('0',64)), "invalid token rejected");
    await controller.Disconnect(id); db.ChangeTracker.Clear();
    Assert(!await Authenticate(token), "disconnection immediately revokes authentication");
    Assert(await db.Set<WooCommerceOrder>().CountAsync() == 103, "disconnect preserves all imported orders");
    await Denied<UnauthorizedAppException>(async () => { await service.Import(id, Payload(9999)); }, "inactive installation cannot import");
    var newToken = WooCommerceSecurity.Secret();
    var reconnect = Json(await controller.Start(new() { InstallationId = installation, StoreName = "Store A", StoreUrl = "https://a.example.test", TokenHash = WooCommerceSecurity.Hash(newToken), PluginVersion = "1.0.0" }));
    await controller.Approve(new() {Code = reconnect.GetProperty("code").GetString()!, DefaultGenderId = 5, DefaultMaterialId = 12, DefaultStatusId = status.Id});
    Assert(await Authenticate(newToken) && !await Authenticate(token), "reauthorization rotates token and retains connection");
    await service.Import(id, Payload()); Assert(await db.Set<WooCommerceOrder>().CountAsync() == 103, "reconnect does not duplicate old orders");
    var multi = Payload(8800); multi.Items[0].Quantity = 2;
    await Task.WhenAll(Enumerable.Range(0, 4).Select(async _ => {
        await using var unitDb = new OMSDbContext(options);
        await new WooCommerceIntegrationService(unitDb).Import(id, multi);
    }));
    var units = await db.Set<WooCommerceOrder>().Include(x => x.Order).Where(x => x.ExternalOrderId == 8800).ToListAsync();
    Assert(units.Count == 2 && units.Select(x => x.OrderId).Distinct().Count() == 2, "quantity two creates exactly two editable rows under concurrent retries");
    Assert(units.Select(x => x.Order.CustomerOrderNumber).Distinct().Count() == 1 && units.All(x => x.Order.CustomerProductTitle == "Shirt"), "unit rows retain the same customer order and product");
    var alternateStatus = new LookupItem { LookupDataTypeId=1, Name="In production", IsActive=true };
    db.Add(alternateStatus); await db.SaveChangesAsync();
    units[1].Order.OrderStatusId=alternateStatus.Id; await db.SaveChangesAsync();
    var mixedUpdates=Json(await Controller(db,0,"",id).Updates(100));
    Assert(mixedUpdates.GetProperty("items").EnumerateArray().Single(x=>x.GetProperty("ExternalOrderId").GetInt64()==8800).GetProperty("status").ValueKind==JsonValueKind.Null, "mixed unit statuses do not prematurely update WooCommerce status");
    units[1].Order.OrderStatusId=status.Id; await db.SaveChangesAsync();
    units[0].Order.CustomerProductTitle = "Keep production edit"; await db.SaveChangesAsync();
    multi.Items[0].Quantity = 3; multi.ModifiedAt = DateTime.UtcNow; await service.Import(id, multi);
    Assert(await db.Set<WooCommerceOrder>().CountAsync(x => x.ExternalOrderId == 8800) == 3 && units[0].Order.CustomerProductTitle == "Keep production edit", "quantity increase adds only missing row and preserves production edits");
    multi.Items[0].Quantity = 1; multi.ModifiedAt = DateTime.UtcNow.AddSeconds(1); await service.Import(id, multi);
    Assert(await db.Set<WooCommerceOrder>().CountAsync(x => x.ExternalOrderId == 8800 && !x.IsCurrentUnit) == 2, "quantity decrease retains extra rows for review");
    multi.Items.Add(new() {Id=2, Name="Coat", Quantity=2}); multi.ModifiedAt=DateTime.UtcNow.AddSeconds(2); await service.Import(id,multi);
    Assert(await db.Set<WooCommerceOrder>().CountAsync(x => x.ExternalOrderId == 8800 && x.IsCurrentUnit) == 3, "multiple products split by line and unit");
    var allUpdates = Json(await Controller(db,0,"",id).Updates(100));
    Assert(allUpdates.GetProperty("items").EnumerateArray().Count(x => x.GetProperty("ExternalOrderId").GetInt64()==8800) == 1, "outbound sync groups units into one WooCommerce update");
    var legacyPayload=Payload(8900); await service.Import(id,legacyPayload);
    var legacyLink=await db.Set<WooCommerceOrder>().SingleAsync(x=>x.ExternalOrderId==8900);
    legacyLink.ExternalLineId=0; await db.SaveChangesAsync(); var legacyId=legacyLink.OrderId;
    legacyPayload.Items[0].Quantity=2; await service.Import(id,legacyPayload);
    Assert(await db.Set<WooCommerceOrder>().CountAsync(x=>x.ExternalOrderId==8900)==2 && legacyLink.OrderId==legacyId, "legacy row expands without replacing its existing order ID");
    legacyPayload.Items[0].ImageUrl="https://store.example.test/product.jpg";
    legacyPayload.Items[0].ImageUrls=new() { "https://store.example.test/product.jpg", "https://store.example.test/back.jpg", "https://store.example.test/side.jpg", "javascript:alert(1)" };
    legacyPayload.ModifiedAt=DateTime.UtcNow.AddMinutes(1); await service.Import(id,legacyPayload);
    var orderReader=new OrderService(db,null!,new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {{"WooCommerce:Enabled","true"}}).Build());
    var imageDetails=await orderReader.GetOrderByIdAsync(legacyId,owner.Id,true);
    Assert(imageDetails.Images.Any(x=>x.Id==0 && x.ImageURL==legacyPayload.Items[0].ImageUrl), "WooCommerce image is exposed as read-only image in order details");
    Assert(imageDetails.Images.Count==3 && imageDetails.Images.All(x=>x.ImageURL.StartsWith("https://")), "gallery images retained, featured duplicate removed and unsafe URLs excluded");
    Assert(!await db.OrderImages.AnyAsync(x=>x.OrderId==legacyId), "external image does not create or overwrite uploaded OMS attachments");
    await ShopifyChecks.Run(db,owner.Id,status.Id,Assert);
    Console.WriteLine($"ALL {checks} CHECKS PASSED");
} finally {
    // This exact GUID database was created by this process; no external database name is accepted.
    if (!database.StartsWith("OMS_Woo_Test_", StringComparison.Ordinal)) throw new Exception("Invalid test database name.");
    await db.Database.EnsureDeletedAsync();
}
