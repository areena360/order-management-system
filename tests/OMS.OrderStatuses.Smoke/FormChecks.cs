using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using OMS_Backend.Common.Exceptions;
using OMS_Backend.Data;
using OMS_Backend.DTOs;
using OMS_Backend.Services;

static class FormChecks
{
    public static async Task Run(OMSDbContext db, IConfiguration config)
    {
        if ((await db.Database.GetPendingMigrationsAsync()).Any()) throw new Exception("Apply pending migrations before form checks.");
        var customer = await db.Users.FirstAsync(u => u.Role.Name == "Customer" && !u.IsDeleted);
        var admin = await db.Users.FirstAsync(u => (u.Role.Name == "Admin" || u.Role.Name == "Super Admin") && !u.IsDeleted);
        var lookups = await db.LookupItems.Where(l => l.IsActive && !l.IsDeleted).ToListAsync();
        foreach (var status in OrderStatusCatalog.Names)
            Check(lookups.Any(l => l.LookupDataTypeId == 1 && l.Name == status), $"Status available: {status}");
        foreach (var priority in new[] { "urgent", "most urgent", "normal", "low" })
            Check(lookups.Any(l => l.LookupDataTypeId == 2 && l.Name == priority), $"Priority available: {priority}");
        int Lookup(int type) => lookups.First(l => l.LookupDataTypeId == type).Id;
        var tag = "form-check-" + Guid.NewGuid().ToString("N");
        var env = new TestEnvironment { ContentRootPath = Path.GetFullPath(Path.Combine("artifacts", tag)) };
        var service = new OrderService(db, env, config);
        CreateOrderDto Input(bool isCustomer) => new() {
            CustomerId = customer.Id, CustomerOrderNumber = tag, CustomerProductTitle = isCustomer ? "Customer title" : null,
            ManufacturerProductTitle = isCustomer ? null : "Manufacturer title", GenderId = Lookup(3),
            ManufacturerMaterialId = isCustomer ? null : Lookup(4), PriorityId = isCustomer ? null : Lookup(2),
            SizeId = Lookup(5), SizeChartId = Lookup(6), ConsigneeName = "Test recipient",
            ConsigneeAddress = "Test address", ShippingEmail = "test@example.test", ShippingContact = "12345",
            Courier = isCustomer ? null : "DHL", TrackingNumber = isCustomer ? null : "TRACK-TEST"
        };
        var ids = new List<int>();
        try
        {
            foreach (var isCustomer in new[] { true, false })
            {
                var actor = isCustomer ? customer.Id : admin.Id;
                foreach (var field in new[] { "CustomerOrderNumber", "ConsigneeName", "ShippingEmail", "ShippingContact", "ConsigneeAddress" }
                    .Concat(isCustomer ? new[] { "CustomerProductTitle" } : new[] { "ManufacturerProductTitle", "Courier", "TrackingNumber" }))
                {
                    var invalid = Input(isCustomer);
                    typeof(CreateOrderDto).GetProperty(field)!.SetValue(invalid, "   ");
                    await Reject(() => service.CreateOrderAsync(invalid, actor, isCustomer), $"{(isCustomer ? "Customer" : "Admin")} requires {field}");
                }
                foreach (var field in new[] { "SizeId", "SizeChartId" }.Concat(isCustomer ? Array.Empty<string>() : new[] { "ManufacturerMaterialId", "PriorityId" }))
                {
                    var invalid = Input(isCustomer);
                    typeof(CreateOrderDto).GetProperty(field)!.SetValue(invalid, null);
                    await Reject(() => service.CreateOrderAsync(invalid, actor, isCustomer), $"{field} cannot be omitted");
                }
                var wrongType = Input(isCustomer);
                wrongType.SizeId = Lookup(3);
                await Reject(() => service.CreateOrderAsync(wrongType, actor, isCustomer), "Cross-type lookup IDs are rejected");
                var input = Input(isCustomer);
                if (isCustomer) { input.Courier = "UPS"; input.ManufacturerOrderNumber = "injected-number"; }
                else { input.ManufacturerOrderNumber = tag; input.NotesByCustomer = "injected notes"; }
                var created = await service.CreateOrderAsync(input, actor, isCustomer);
                ids.Add(created.Id);
                Check(created.Status == (isCustomer ? "new" : "assign"), "Correct default status on create");
                Check(created.CustomerMaterialId == null, "Optional customer material persists blank");
                Check(isCustomer ? created.Courier == null && created.ManufacturerOrderNumber != "injected-number"
                    : created.CustomerProductTitle == "" && created.ManufacturerOrderNumber == tag && created.NotesByCustomer == null,
                    "Role-specific optional and disabled create fields enforced");
            }

            await using (var tx = await db.Database.BeginTransactionAsync())
            {
                var order = await db.Orders.SingleAsync(o => o.Id == ids[1]);
                order.NotesByCustomer = "Original customer notes";
                order.NotesByManufacturer = "Original manufacturer notes";
                await db.SaveChangesAsync();
                UpdateOrderDto Update() => new() {
                    CustomerId = customer.Id, CustomerOrderNumber = tag, CustomerProductTitle = "Customer title",
                    ManufacturerProductTitle = "Manufacturer title", GenderId = Lookup(3), ManufacturerMaterialId = Lookup(4),
                    PriorityId = Lookup(2), SizeId = Lookup(5), SizeChartId = Lookup(6), ConsigneeName = "Test recipient",
                    ConsigneeAddress = "Test address", ShippingEmail = "test@example.test", ShippingContact = "12345",
                    Courier = "DHL", TrackingNumber = "TRACK-TEST"
                };
                var update = Update();
                update.Courier = null;
                update.TrackingNumber = "injected tracking";
                update.NotesByManufacturer = "injected notes";
                update.ManufacturerOrderNumber = "injected-number";
                var saved = await service.UpdateOrderAsync(order.Id, update, customer.Id, true);
                Check(saved.Courier == "DHL" && saved.TrackingNumber == "TRACK-TEST" && saved.NotesByManufacturer == "Original manufacturer notes"
                    && saved.ManufacturerOrderNumber == tag, "Customer update preserves disabled shipping and manufacturer fields");
                order.NotesByCustomer = "Preserve this";
                await db.SaveChangesAsync();
                update = Update(); update.NotesByCustomer = "injected notes";
                saved = await service.UpdateOrderAsync(order.Id, update, admin.Id, false);
                Check(saved.NotesByCustomer == "Preserve this", "Admin update preserves disabled customer notes");

                foreach (var fileName in new[] { "bill.xlsx", "bill.docx", "bill.zip", "bill.html", "bill" })
                {
                    using var content = new MemoryStream("test content"u8.ToArray());
                    var file = new FormFile(content, 0, content.Length, "billImageFile", fileName);
                    var bill = await service.AddInventoryBillAsync(order.Id, new SaveInventoryBillDto { BillDetails = "Test bill", BillImageFile = file }, admin.Id, false);
                    Check(bill.BillImage!.EndsWith(".download"), $"{fileName} stored with an inert download extension");
                    var edited = await service.UpdateInventoryBillAsync(order.Id, bill.Id, new SaveInventoryBillDto { BillDetails = "Edited bill", BillNumber = 123 }, admin.Id, false);
                    Check(edited.BillImage == bill.BillImage && edited.BillDetails == "Edited bill", "Bill edit preserves attachment");
                }
                try { await service.AddInventoryBillAsync(order.Id, new SaveInventoryBillDto { BillDetails = "Forbidden" }, customer.Id, true); throw new Exception("Customer could add bill"); }
                catch (ForbiddenAppException) { Console.WriteLine("PASS: Customer cannot manage inventory bills"); }
                await tx.RollbackAsync();
            }
        }
        finally
        {
            db.ChangeTracker.Clear();
            // Only remove disposable orders created by this exact test run.
            var testIds = await db.Orders.Where(o => ids.Contains(o.Id) && o.CustomerOrderNumber == tag).Select(o => o.Id).ToArrayAsync();
            await db.OrderStatusHistories.Where(h => testIds.Contains(h.OrderId)).ExecuteDeleteAsync();
            await db.Orders.Where(o => testIds.Contains(o.Id)).ExecuteDeleteAsync();
        }
        Check(!await db.Orders.AnyAsync(o => ids.Contains(o.Id)), "Disposable test orders removed; bill tests rolled back");
    }

    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); Console.WriteLine("PASS: " + message); }
    static async Task Reject(Func<Task> action, string message)
    {
        try { await action(); } catch (ValidationAppException) { Console.WriteLine("PASS: " + message); return; }
        throw new Exception(message);
    }
    sealed class TestEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "FormChecks";
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
