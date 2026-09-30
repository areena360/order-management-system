using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OMS_Backend.Data;
using OMS_Backend.DTOs;
using OMS_Backend.Services;

static class LiveAssignmentChecks
{
    // Program.cs already rejects non-local databases. Tokens stay in memory and are never printed.
    public static async Task Run(OMSDbContext db, IConfiguration config, int roleId, bool enable)
    {
        var role = await db.Roles.SingleAsync(r => r.Id == roleId && r.IsActive);
        if (role.Name is "Customer" or "Super Admin") throw new InvalidOperationException("Choose an assignable staff role.");
        var users = await db.Users.Where(u => u.RoleId == roleId && u.IsActive && !u.IsDeleted).ToListAsync();
        if (users.Count == 0) throw new InvalidOperationException("No active users in the selected role.");
        using var handler = new HttpClientHandler {
            ServerCertificateCustomValidationCallback = (request, _, _, _) => request.RequestUri?.IsLoopback == true
        };
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://localhost:44370/api/") };
        var jwt = new JwtService(config);
        void Authenticate(User user, string name) => client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", jwt.GenerateToken(user, name).token);
        foreach (var user in users)
        {
            Authenticate(user, role.Name);
            var before = await client.GetFromJsonAsync<PagedResult<OrderListDto>>("orders?pageSize=200");
            Console.WriteLine($"BEFORE: role={role.Name}, userId={user.Id}, API orders={before!.TotalCount}");
        }
        if (enable)
        {
            var admin = await db.Users.FirstAsync(u => u.Role != null && u.Role.Name == "Super Admin" && u.IsActive && !u.IsDeleted);
            Authenticate(admin, "Super Admin");
            var response = await client.PutAsJsonAsync("rolepermissions", new SaveRolePermissionsDto {
                RoleId = roleId, Permissions = [new() { ScreenKey = "Orders", AdminAssignedOnly = true }]
            });
            response.EnsureSuccessStatusCode();
            Console.WriteLine($"SAVED: assigned-only order access for role {role.Name}");
        }
        foreach (var user in users)
        {
            Authenticate(user, role.Name);
            var expected = await db.AdminOrderAssignments.Where(a => a.UserId == user.Id && a.RoleId == user.RoleId
                && !a.Order.IsDeleted && (!a.Order.RequiresCustomerAssignment || a.Order.IsAssigned))
                .Select(a => a.OrderId).ToListAsync();
            var actual = new List<int>();
            for (var page = 1; ; page++)
            {
                var result = (await client.GetFromJsonAsync<PagedResult<OrderListDto>>($"orders?pageSize=200&pageNumber={page}"))!;
                actual.AddRange(result.Items.Select(o => o.Id));
                if (actual.Count >= result.TotalCount || result.Items.Count == 0) break;
            }
            if (!actual.Order().SequenceEqual(expected.Order())) throw new Exception("Live API returned a different set from this user's assignments.");
            Console.WriteLine($"PASS: role={role.Name}, userId={user.Id}, API returns exactly assigned order IDs [{string.Join(",", actual)}]");
            var unassigned = await db.Orders.Where(o => !o.IsDeleted && !expected.Contains(o.Id)).Select(o => o.Id).FirstOrDefaultAsync();
            if (unassigned != 0)
            {
                var denied = await client.GetAsync($"orders/{unassigned}");
                if (denied.StatusCode != System.Net.HttpStatusCode.NotFound) throw new Exception("Unassigned details were not denied.");
                Console.WriteLine("PASS: live API rejects unassigned order details");
            }
        }
    }
}
