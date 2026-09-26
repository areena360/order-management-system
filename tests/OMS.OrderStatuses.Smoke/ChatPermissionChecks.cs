using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OMS_Backend.Common.Exceptions;
using OMS_Backend.Controllers;
using OMS_Backend.Data;
using OMS_Backend.DTOs;
using OMS_Backend.Hubs;
using OMS_Backend.Services;

static class ChatPermissionChecks
{
    public static async Task Run(OMSDbContext db)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var staff = await db.Users.Include(u => u.Role).FirstAsync(u => !u.IsDeleted && u.Role!.Name != "Customer" && u.Role.Name != "Super Admin" && u.Role.Name != "Admin");
        staff.IsActive = true;
        var admin = await db.Users.Include(u => u.Role).FirstAsync(u => !u.IsDeleted && u.IsActive && u.Role!.Name == "Super Admin");
        var order = await db.Orders.FirstAsync(o => !o.IsDeleted && (!o.RequiresCustomerAssignment || o.IsAssigned));
        await db.SaveChangesAsync();
        var service = new ChatService(db);
        using var provider = new ServiceCollection().AddLogging().AddSignalR().Services.BuildServiceProvider();
        var controller = new RolePermissionsController(db, provider.GetRequiredService<IHubContext<ChatHub>>()) {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("userId", admin.Id.ToString()) }, "test"))
            }}
        };
        var rows = (IEnumerable<RolePermissionDto>)((OkObjectResult)await controller.GetByRole(staff.RoleId!.Value)).Value!;
        Check(rows.Any(p => p.ScreenKey == ChatAccess.CustomerScreen) && rows.Any(p => p.ScreenKey == ChatAccess.GroupScreen), "Manage Roles exposes both chat channels");
        async Task Set(bool customerView, bool customerSend, bool groupView, bool groupSend)
        {
            var result = await controller.Save(new SaveRolePermissionsDto { RoleId = staff.RoleId.Value, Permissions = new() {
                new() { ScreenKey = ChatAccess.CustomerScreen, CanView = customerView, CanAdd = customerSend },
                new() { ScreenKey = ChatAccess.GroupScreen, CanView = groupView, CanAdd = groupSend }
            }});
            Check(result is OkObjectResult, "Admin can save chat controls");
        }
        await Set(false, true, false, true);
        Check(!await ChatAccess.CanAccess(db, staff.Id, false) && !await ChatAccess.CanAccess(db, staff.Id, true), "Hidden channels deny view even with Send requested");
        await Denied(() => service.GetConversationAsync(order.Id, staff.Id, false));
        await Denied(() => service.GetGroupConversationAsync(order.Id, staff.Id));
        Check(!(await service.GetUnreadAsync(staff.Id, false)).Any(), "Hidden channels expose no unread counts");
        Check(!(await service.GetRecipientGroupsAsync(order.Id, false)).Contains($"user_{staff.Id}") && !(await service.GetRecipientGroupsAsync(order.Id, true)).Contains($"user_{staff.Id}"), "Hidden role excluded from both live broadcasts");
        await Set(true, false, true, false);
        await service.GetConversationAsync(order.Id, staff.Id, false);
        await service.GetGroupConversationAsync(order.Id, staff.Id);
        await Denied(() => service.SaveMessageAsync(order.Id, staff.Id, staff.Role!.Name, false, "must be blocked"));
        await Denied(() => service.SaveGroupMessageAsync(order.Id, staff.Id, staff.Role!.Name, "must be blocked"));
        Check((await service.GetRecipientGroupsAsync(order.Id, true)).Contains($"user_{staff.Id}"), "View-only role receives messages");
        await Set(true, true, true, true);
        await service.SaveMessageAsync(order.Id, staff.Id, staff.Role!.Name, false, "permission test");
        await service.SaveGroupMessageAsync(order.Id, staff.Id, staff.Role!.Name, "permission test");
        Check(true, "View and Send allow both channels");
        await Set(false, false, true, true);
        Check(!await ChatAccess.CanAccess(db, staff.Id, false) && await ChatAccess.CanAccess(db, staff.Id, true, true), "Channels are independent after revocation");
        Check(!(await service.GetRecipientGroupsAsync(order.Id, false)).Contains($"user_{staff.Id}"), "Existing user group stops receiving revoked channel");
        controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("userId", staff.Id.ToString()) }, "test"));
        Check(await controller.Save(new() { RoleId = staff.RoleId.Value }) is ForbidResult, "Non-admin cannot change role permissions");
        await tx.RollbackAsync();
        Console.WriteLine("PASS: all chat permission tests rolled back; user permissions unchanged.");
    }
    static async Task Denied(Func<Task> action)
    {
        try { await action(); throw new Exception("Access should have been denied"); }
        catch (ForbiddenAppException) { Console.WriteLine("PASS: server rejects unauthorized chat action"); }
    }
    static void Check(bool success, string message) { if (!success) throw new Exception(message); Console.WriteLine("PASS: " + message); }
}
