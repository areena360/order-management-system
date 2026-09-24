using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using OMS_Backend.Common.Exceptions;
using OMS_Backend.Data;
using OMS_Backend.DTOs;
using OMS_Backend.Migrations;
using OMS_Backend.Services;

static class AssignmentChecks
{
    public static async Task Run(OMSDbContext db, IConfiguration config, bool apply)
    {
        const string target = "20260924112459_OrderAssignmentVisibility";
        var pending = (await db.Database.GetPendingMigrationsAsync()).ToArray();
        if (pending.Any(id => id != target)) throw new Exception("Apply prior migrations first.");
        var beforeCount = await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM Orders").SingleAsync();
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            if (pending.Contains(target))
                foreach (var command in db.GetService<IMigrationsSqlGenerator>().Generate(new OrderAssignmentVisibility().UpOperations))
                    await db.Database.ExecuteSqlRawAsync(command.CommandText);
            await CheckSchema(db);
            var customer = await db.Users.FirstAsync(u => u.RoleId == 4 && !u.IsDeleted);
            var staff = await db.Users.FirstAsync(u => (u.RoleId == 1 || u.RoleId == 2) && !u.IsDeleted);
            var status = await db.LookupItems.Where(OrderStatusCatalog.Selectable).FirstAsync(x => x.Name == OrderStatusCatalog.Assign);
            var gender = await db.LookupItems.FirstAsync(x => x.LookupDataTypeId == 3);
            var material = await db.LookupItems.FirstAsync(x => x.LookupDataTypeId == 4);
            var tag = "assignment-test-" + Guid.NewGuid().ToString("N");
            Order NewOrder(bool draft) => new() {
                CustomerId = customer.Id, CreatedBy = draft ? customer.Id : staff.Id,
                RequiresCustomerAssignment = draft, IsActive = true,
                ManufacturerOrderNumber = tag + (draft ? "-draft" : "-staff"),
                CustomerProductTitle = tag, ConsigneeName = "Test", ConsigneeAddress = "Test",
                GenderId = gender.Id, CustomerMaterialId = material.Id, ManufacturerMaterialId = material.Id,
                IsCustomSize = true, SizeDetails = "Test", OrderStatusId = status.Id
            };
            var draft = NewOrder(true);
            var staffOrder = NewOrder(false);
            db.AddRange(draft, staffOrder);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var service = new OrderService(db, null!, config);
            var query = new OrderQueryDto { Search = tag };
            var adminList = await service.GetOrdersAsync(query, staff.Id, false);
            Check(adminList.Items.Count == 1 && adminList.Items[0].Id == staffOrder.Id, "Admin sees staff-created orders but not customer drafts");
            Check((await service.GetOrdersAsync(query, customer.Id, true)).TotalCount == 2, "Customer sees their own drafts and staff-created orders");
            await NotFound(() => service.GetOrderByIdAsync(draft.Id, staff.Id, false), "Direct details access blocked for customer draft");
            await NotFound(() => service.GetInventoryBillsAsync(draft.Id, staff.Id, false), "Draft inventory access blocked");
            await NotFound(() => service.UpdateOrderStatusAsync(draft.Id, new UpdateOrderStatusDto { StatusId = status.Id }, staff.Id, false), "Draft status edit blocked");
            await NotFound(() => new ChatService(db).GetConversationAsync(draft.Id, staff.Id, false), "Draft chat access blocked");
            await NotFound(() => new ChatService(db).SaveMessageAsync(draft.Id, customer.Id, "Customer", true, "hidden draft"), "Draft cannot send staff notifications");
            await NotFound(() => service.GetOrderByIdAsync(draft.Id, staff.Id, true), "Another customer cannot read the draft");
            try {
                await service.AssignOrdersAsync(new AssignOrdersDto { OrderIds = [draft.Id] }, staff.Id, false);
                throw new Exception("Admin assigned a customer draft.");
            } catch (ForbiddenAppException) { Console.WriteLine("PASS: only customer can release the draft"); }
            Check((await service.AssignOrdersAsync(new AssignOrdersDto { OrderIds = [draft.Id] }, staff.Id, true)).AssignedCount == 0,
                "Another customer cannot assign the draft");
            var before = DateTime.UtcNow;
            Check((await service.AssignOrdersAsync(new AssignOrdersDto { OrderIds = [draft.Id] }, customer.Id, true)).AssignedCount == 1, "Customer assignment succeeds");
            db.ChangeTracker.Clear();
            var released = await service.GetOrderByIdAsync(draft.Id, staff.Id, false);
            Check(released.AssignedDate >= before && released.AssignedDate <= DateTime.UtcNow && released.DaysForMaking == 0,
                "Server sets assignment date and zero Days Passed");
            await service.AssignOrdersAsync(new AssignOrdersDto { OrderIds = [draft.Id] }, customer.Id, true);
            db.ChangeTracker.Clear();
            Check((await service.GetOrderByIdAsync(draft.Id, staff.Id, false)).AssignedDate == released.AssignedDate,
                "Retry preserves original assignment date");
            Check((await service.GetOrdersAsync(query, staff.Id, false)).TotalCount == 2, "Assigned order becomes visible to admin");
            var dateQuery = new OrderQueryDto { Search = tag, DateFrom = DateTime.UtcNow.Date, DateTo = DateTime.UtcNow.Date };
            Check((await service.GetOrdersAsync(dateQuery, staff.Id, false)).Items.Single().Id == draft.Id, "Date filter uses assignment time");
            await tx.RollbackAsync();
        }
        db.ChangeTracker.Clear();
        Check(await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM Orders").SingleAsync() == beforeCount, "Test orders rolled back");
        if (apply)
        {
            await db.Database.MigrateAsync();
            await CheckSchema(db);
            Check(!(await db.Database.GetPendingMigrationsAsync()).Any(), "Assignment migration recorded as applied");
            Console.WriteLine("APPLIED: assignment visibility and removal of Orders audit-date columns.");
        }
    }

    static async Task CheckSchema(OMSDbContext db)
    {
        var count = await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM sys.columns WHERE object_id = OBJECT_ID('Orders') AND name IN ('CreatedDate','UpdatedDate')").SingleAsync();
        Check(count == 0, "Orders CreatedDate and UpdatedDate columns removed");
    }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("PASS: " + message); }
    static async Task NotFound(Func<Task> action, string message)
    {
        try { await action(); } catch (NotFoundException) { Console.WriteLine("PASS: " + message); return; }
        throw new Exception(message);
    }
}
