using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OMS_Backend.Data;
using OMS_Backend.DTOs;
using OMS_Backend.Migrations;
using OMS_Backend.Models;
using OMS_Backend.Services;

static class ChatReadChecks
{
    public static async Task Run(OMSDbContext db, bool apply)
    {
        const string target = "20260926102804_AddChatReadStates";
        var pending = !(await db.Database.GetAppliedMigrationsAsync()).Contains(target);
        async Task CreateTable()
        {
            foreach (var command in db.GetService<IMigrationsSqlGenerator>().Generate(new AddChatReadStates().UpOperations))
                await db.Database.ExecuteSqlRawAsync(command.CommandText);
        }
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            if (pending) await CreateTable();
            var order = await db.Orders.FirstAsync(o => !o.IsDeleted && (!o.RequiresCustomerAssignment || o.IsAssigned));
            var staff = await db.Users.FirstAsync(u => u.Id != order.CustomerId && u.Role!.Name == "Super Admin" && !u.IsDeleted);
            var service = new ChatService(db);
            var first = await service.SaveMessageAsync(order.Id, staff.Id, "Admin", false, "Read-state regression test");
            var unread = await service.GetUnreadAsync(order.CustomerId, true);
            Check(unread.Any(r => r.OrderId == order.Id && r.Channel == "Customer" && r.Count > 0), "Unread survives a fresh server query");
            await service.MarkReadAsync(order.Id, order.CustomerId, true, new("Customer", first.Id));
            Check(!(await service.GetUnreadAsync(order.CustomerId, true)).Any(r => r.OrderId == order.Id), "Read acknowledgement clears only displayed messages");
            var second = await service.SaveMessageAsync(order.Id, staff.Id, "Admin", false, "New message after read");
            Check((await service.GetUnreadAsync(order.CustomerId, true)).Single(r => r.OrderId == order.Id).Count == 1, "Later message stays unread");
            await service.MarkReadAsync(order.Id, order.CustomerId, true, new("Customer", second.Id));
            await service.MarkReadAsync(order.Id, order.CustomerId, true, new("Customer", first.Id));
            Check(await db.ChatReadStates.AnyAsync(r => r.UserId == order.CustomerId && r.OrderId == order.Id && r.LastReadMessageId == second.Id), "Older tabs cannot reverse read progress");
            Check(!await db.ChatReadStates.AnyAsync(r => r.UserId == staff.Id && r.OrderId == order.Id && r.LastReadMessageId == second.Id), "Read progress is per user");
            var group = await service.SaveGroupMessageAsync(order.Id, staff.Id, "Super Admin", "Group isolation test");
            Check(!(await service.GetUnreadAsync(order.CustomerId, true)).Any(r => r.OrderId == order.Id), "Customer cannot see group unread counts");
            Check(!await db.ChatReadStates.AnyAsync(r => r.UserId == order.CustomerId && r.Channel == "Group"), "Customer read does not mark group read");
            await tx.RollbackAsync();
        }
        db.ChangeTracker.Clear();
        if (apply && pending)
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            await CreateTable();
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES ({target}, {"9.0.0"})");
            await tx.CommitAsync();
            Console.WriteLine("APPLIED: only ChatReadStates table and migration record; existing messages unchanged.");
        }
    }
    static void Check(bool result, string description)
    {
        if (!result) throw new Exception(description);
        Console.WriteLine("PASS: " + description);
    }
}

