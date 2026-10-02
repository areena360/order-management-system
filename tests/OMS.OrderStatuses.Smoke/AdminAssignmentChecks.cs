using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using OMS_Backend.Common.Exceptions;
using OMS_Backend.Controllers;
using OMS_Backend.Data;
using OMS_Backend.DTOs;
using OMS_Backend.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using OMS_Backend.Hubs;

static class AdminAssignmentChecks
{
    public static async Task Run(OMSDbContext db, IConfiguration config, bool apply)
    {
        var pending = (await db.Database.GetPendingMigrationsAsync()).ToArray();
        if (pending.Any(id => !id.EndsWith("_AdminOrderAssignments") && !id.EndsWith("_ManufacturingTimeline") && !id.EndsWith("_AssignmentMessages")))
            throw new Exception("Apply prior migrations before this assignment test.");
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            var assembly = db.GetService<IMigrationsAssembly>();
            foreach (var id in pending)
            {
                var migration = assembly.CreateMigration(assembly.Migrations[id], db.Database.ProviderName!);
                foreach (var command in db.GetService<IMigrationsSqlGenerator>().Generate(migration.UpOperations))
                    await db.Database.ExecuteSqlRawAsync(command.CommandText);
            }
            var tag = "admin-assignment-" + Guid.NewGuid().ToString("N");
            var role = new Role { Name = tag, IsActive = true };
            db.Roles.Add(role);
            await db.SaveChangesAsync();
            User NewUser(string suffix) => new() { FirstName = "Assignment", LastName = suffix,
                Email = tag + suffix + "@example.test", FirstContact = "test", Password = "unused",
                RoleId = role.Id, IsActive = true, ApprovalStatus = "Approved" };
            var first = NewUser("one");
            var second = NewUser("two");
            db.Users.AddRange(first, second);
            db.RolePermissions.Add(new RolePermission { RoleId = role.Id, ScreenKey = "Orders",
                CanView = true, IsActive = true });
            await db.SaveChangesAsync();
            var admin = await db.Users.FirstAsync(u => u.Role != null && u.Role.Name == "Super Admin" && u.IsActive && !u.IsDeleted);
            var customer = await db.Users.FirstAsync(u => u.Role != null && u.Role.Name == "Customer" && u.IsActive && !u.IsDeleted);
            var status = await db.LookupItems.FirstAsync(OrderStatusCatalog.Selectable);
            var gender = await db.LookupItems.FirstAsync(x => x.LookupDataTypeId == 3);
            Order NewOrder(string suffix) => new() { ManufacturerOrderNumber = tag + suffix, CustomerId = customer.Id,
                Amount = 987654, CustomerOrderNumber = "Private Reference", TrackingNumber = "Private Tracking",
                ShippingEmail = "private@example.test", ShippingContact = "Private Phone", Courier = "Private Courier",
                CustomerProductTitle = tag, ConsigneeName = "Test", ConsigneeAddress = "Test", GenderId = gender.Id,
                OrderStatusId = status.Id, RequiresCustomerAssignment = false, IsActive = true, IsCustomSize = true, SizeDetails = "Test" };
            var order = NewOrder("-one");
            var other = NewOrder("-two");
            db.Orders.AddRange(order, other);
            await db.SaveChangesAsync();
            var service = new OrderService(db, null!, config);
            var query = new OrderQueryDto { Search = tag };
            Check((await service.GetOrdersAsync(query, first.Id, false)).TotalCount == 2, "User initially has general order access");
            using var provider = new ServiceCollection().AddLogging().AddSignalR().Services.BuildServiceProvider();
            var permissionApi = new RolePermissionsController(db, provider.GetRequiredService<IHubContext<ChatHub>>()) {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("userId", admin.Id.ToString()) }, "test"))
                }}
            };
            Check(await permissionApi.Save(new() { RoleId = role.Id, Permissions = [new() {
                ScreenKey = "Orders", AdminAssignedOnly = true, CanView = true, CanAdd = true, CanEdit = true, CanDelete = true
            }] }) is OkObjectResult, "Saving assigned-only permission through the real API succeeds");
            var savedPermission = await db.RolePermissions.AsNoTracking().SingleAsync(p => p.RoleId == role.Id && p.ScreenKey == "Orders");
            Check(savedPermission.AdminAssignedOnly && !savedPermission.CanView && !savedPermission.CanAdd
                && !savedPermission.CanEdit && !savedPermission.CanDelete, "API persists restriction and clears general access");
            Check((await service.GetOrdersAsync(query, first.Id, false)).TotalCount == 0, "Restricted users initially see no orders");

            AdminOrderAssignmentsController Controller(int id) => new(db) { ControllerContext = new ControllerContext {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("userId", id.ToString()) }, "test")) }
            }};
            var adminApi = Controller(admin.Id);
            Check(await adminApi.Save(order.Id, new() { UserIds = [first.Id, second.Id] }) is OkObjectResult, "Admin assigns multiple users");
            Check(await db.ManufacturingEvents.CountAsync(e => e.OrderId == order.Id && e.Status == "assigned") == 2, "Timeline records each assigned member");
            Check(await adminApi.Save(order.Id, new() { UserIds = [first.Id, second.Id], Messages = new() { [first.Id] = "Cut carefully" } }) is OkObjectResult, "Save personal message");
            Check((await service.GetOrdersAsync(query, first.Id, false)).Items.Single().AssignmentMessageUnread, "New message is unread");
            Check(!(await service.GetOrdersAsync(query, second.Id, false)).Items.Single().HasAssignmentMessage, "Message is private to recipient");
            Check(await Controller(second.Id).ReadMessage(order.Id) is NotFoundResult, "Other assignee cannot read message");
            Check(await Controller(first.Id).ReadMessage(order.Id) is OkObjectResult, "Recipient opens message");
            db.ChangeTracker.Clear();
            Check(!(await service.GetOrdersAsync(query, first.Id, false)).Items.Single().AssignmentMessageUnread, "Read state persists");
            await adminApi.Save(order.Id, new() { UserIds = [first.Id, second.Id], Messages = new() { [first.Id] = "Cut carefully" } });
            Check(!(await service.GetOrdersAsync(query, first.Id, false)).Items.Single().AssignmentMessageUnread, "Unchanged message stays read");
            await adminApi.Save(order.Id, new() { UserIds = [first.Id, second.Id], Messages = new() { [first.Id] = "Updated instructions" } });
            Check((await service.GetOrdersAsync(query, first.Id, false)).Items.Single().AssignmentMessageUnread, "Edited message becomes unread");
            Check(await adminApi.Manufacturing(order.Id) is OkObjectResult, "Admin can read manufacturing history");
            Check(await Controller(first.Id).Manufacturing(order.Id) is ForbidResult, "Team history is admin-only");
            foreach (var user in new[] { first, second })
            {
                var list = await service.GetOrdersAsync(query, user.Id, false);
                Check(list.TotalCount == 1 && list.Items.Single().Id == order.Id && list.Items.Single().AssignmentStatus == "assigned", "Each assignee sees only their assigned order");
                Check(list.Items.Single().Amount == null && list.Items.Single().CustomerName == ""
                    && list.Items.Single().CustomerOrderNumber == null && list.Items.Single().TrackingNumber == null
                    && list.Items.Single().Status == "" && list.Items.Single().AssignedUserIds.Count == 0,
                    "Assigned list redacts business fields");
                var detail = await service.GetOrderByIdAsync(order.Id, user.Id, false);
                Check(detail.Amount == null && detail.CustomerId == 0 && detail.CustomerName == ""
                    && detail.CustomerOrderNumber == null && detail.ConsigneeName == "" && detail.ConsigneeAddress == ""
                    && detail.ShippingEmail == null && detail.ShippingContact == null && detail.Courier == null
                    && detail.TrackingNumber == null && detail.Status == "" && detail.StatusHistory.Count == 0
                    && detail.InventoryBills.Count == 0, "Assigned details redact business fields");
                Check(detail.CustomerProductTitle == tag && detail.SizeDetails == "Test" && detail.AssignmentStatus == "assigned",
                    "Production details remain visible");
                Check((await service.GetInventoryBillsAsync(order.Id, user.Id, false)).Count == 0, "Assigned bill endpoint is redacted");
                Check((await service.GetOrdersAsync(new() { Search = "Private Reference" }, user.Id, false)).TotalCount == 0,
                    "Search cannot reveal hidden customer references");
                await Expect<NotFoundException>(() => service.GetOrderByIdAsync(other.Id, user.Id, false), "Unassigned detail URL denied");
            }
            Check((await service.GetOrderByIdAsync(order.Id, admin.Id, false)).Amount == 987654, "Admin retains amount visibility");
            Check((await service.GetOrdersAsync(query, admin.Id, false)).TotalCount == 2, "Admin keeps full order visibility");
            Check(await Controller(first.Id).Save(order.Id, new() { UserIds = [first.Id] }) is ForbidResult, "Assignee cannot change assignments");
            Check(await adminApi.Save(order.Id, new() { UserIds = [customer.Id] }) is BadRequestObjectResult, "Customers cannot be assigned");
            Check(await Controller(first.Id).UpdateStatus(order.Id, new() { Status = "inprogress" }) is OkObjectResult, "Assignee updates own progress");
            Check(await Controller(first.Id).UpdateStatus(order.Id, new() { Status = "done" }) is OkObjectResult, "Assignee marks own work done");
            await Controller(first.Id).UpdateStatus(order.Id, new() { Status = "done" });
            var history = await db.ManufacturingEvents.Where(e => e.OrderId == order.Id && e.UserId == first.Id).OrderBy(e => e.Id).ToListAsync();
            Check(history.Select(e => e.Status).SequenceEqual(new[] { "assigned", "inprogress", "done" }), "Timeline preserves status sequence without duplicate events");
            Check(history.All(e => e.RoleId == role.Id && e.RoleName == role.Name && e.UserName == "Assignment one" && !e.IsSnapshot), "Timeline includes member and role snapshots");
            Check(await Controller(first.Id).UpdateStatus(order.Id, new() { Status = "invalid" }) is BadRequestObjectResult, "Invalid progress rejected");
            Check(await Controller(first.Id).UpdateStatus(other.Id, new() { Status = "done" }) is NotFoundResult, "Unassigned progress update denied");
            Check((await service.GetOrdersAsync(query, second.Id, false)).Items.Single().AssignmentStatus == "assigned", "Progress is independent per user");
            Check((await db.Orders.FindAsync(order.Id))!.OrderStatusId == status.Id, "Main order status is unchanged");
            await Expect<ForbiddenAppException>(() => service.CreateOrderAsync(new(), first.Id, false), "Create denied");
            await Expect<ForbiddenAppException>(() => service.UpdateOrderAsync(order.Id, new(), first.Id, false), "Edit denied");
            await Expect<ForbiddenAppException>(() => service.DeleteOrderAsync(order.Id, first.Id, false), "Delete denied");
            await Expect<ForbiddenAppException>(() => service.UpdateOrderStatusAsync(order.Id, new() { StatusId = status.Id }, first.Id, false), "Main status edit denied");
            await Expect<ForbiddenAppException>(() => service.AddOrderImagesAsync(order.Id, [], first.Id, false), "Image mutation denied");
            await Expect<ForbiddenAppException>(() => service.AddInventoryBillAsync(order.Id, new(), first.Id, false), "Bill mutation denied");
            Check(await adminApi.Save(order.Id, new() { UserIds = [first.Id] }) is OkObjectResult, "Admin removes an assignee");
            Check((await service.GetOrdersAsync(query, second.Id, false)).TotalCount == 0, "Unassignment revokes access");
            Check((await service.GetOrdersAsync(query, first.Id, false)).Items.Single().AssignmentStatus == "done", "Retained assignments preserve progress");
            Check(await adminApi.Save(order.Id, new() { UserIds = [] }) is OkObjectResult, "All assignments can be cleared");
            Check((await service.GetOrdersAsync(query, first.Id, false)).TotalCount == 0, "Clearing assignments removes visibility");
            Check(await db.ManufacturingEvents.CountAsync(e => e.OrderId == order.Id) == 6, "Removing team members preserves history and records removals");
            await adminApi.Save(order.Id, new() { UserIds = [first.Id] });
            Check(await db.ManufacturingEvents.CountAsync(e => e.OrderId == order.Id && e.UserId == first.Id) == 5, "Reassignment adds a new event to retained history");
            await tx.RollbackAsync();
        }
        db.ChangeTracker.Clear();
        if (apply) { await db.Database.MigrateAsync(); Console.WriteLine("APPLIED: Assignment and manufacturing migrations."); }
    }

    private static void Check(bool condition, string label)
    {
        if (!condition) throw new Exception(label);
        Console.WriteLine("PASS: " + label);
    }
    private static async Task Expect<T>(Func<Task> action, string label) where T : Exception
    {
        try { await action(); } catch (T) { Check(true, label); return; }
        throw new Exception(label);
    }
}
