using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OMS_Backend.Data;
using OMS_Backend.Services;

internal static class LocalDemo
{
    public static async Task Prepare()
    {
        var root = Directory.GetCurrentDirectory();
        if (!Directory.Exists(Path.Combine(root, "OMS_Backend"))) throw new Exception("Run from repository root.");
        var file = Path.Combine(root, "artifacts", "local-demo.json");
        if (File.Exists(file)) { Console.WriteLine("Local demo already configured; existing credentials and database retained."); return; }
        var name = "OMS_Woo_Local_" + Guid.NewGuid().ToString("N");
        var connection = $"Server=(localdb)\\MSSQLLocalDB;Database={name};Integrated Security=true;TrustServerCertificate=true";
        await using var db = new OMSDbContext(new DbContextOptionsBuilder<OMSDbContext>().UseSqlServer(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var password = "Demo!9" + WooCommerceSecurity.Secret()[..18];
        var admin = new User { FirstName = "Local", LastName = "Admin", Email = "admin@oms.example.test", RoleId = 1, IsActive = true, FirstContact = "0000000000" };
        var customer = new User { FirstName = "Local", LastName = "Store", Email = "store@oms.example.test", RoleId = 4, IsActive = true, FirstContact = "0000000000" };
        var hasher = new PasswordHasher<User>();
        admin.Password = hasher.HashPassword(admin, password); customer.Password = hasher.HashPassword(customer, password);
        db.AddRange(admin, customer);
        foreach (var status in new[] { "New", "In Production", "Shipped", "Cancelled" })
            db.Add(new LookupItem { Name = status, LookupDataTypeId = 1, IsActive = true });
        db.Add(new RolePermission { RoleId = 4, ScreenKey = "Orders", CanView = true, CanAdd = true, CanEdit = true, IsActive = true });
        await db.SaveChangesAsync();
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        await File.WriteAllTextAsync(file, JsonSerializer.Serialize(new {
            connectionString = connection, jwtSecret = WooCommerceSecurity.Secret(), adminEmail = admin.Email,
            customerEmail = customer.Email, password, customerId = customer.Id,
            frontendUrl = "http://localhost:4201", apiUrl = "http://localhost:5511/api", wordpressUrl = "http://localhost:8088"
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("Created isolated local demo. Credentials saved in artifacts/local-demo.json (gitignored).");
    }
}
