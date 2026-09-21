using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using OMS_Backend.Data;
using OMS_Backend.Models;

internal static class MigrationSafety
{
    public static async Task Verify(DbContextOptions<OMSDbContext> options)
    {
        await using var baseline = new BeforeIntegrationContext(options);
        await baseline.Database.EnsureCreatedAsync();
        var user = new User { FirstName = "Existing", LastName = "Customer", Email = "existing@example.test", FirstContact = "0", Password = "fixture", RoleId = 4, IsActive = true };
        var order = new Order { Customer = user, CustomerProductTitle = "Existing production order", ManufacturerOrderNumber = "AD1001", ConsigneeName = "Existing buyer", ConsigneeAddress = "Existing address", Amount = 750, TrackingNumber = "EXISTING-TRACK" };
        baseline.Add(order); await baseline.SaveChangesAsync();
        await baseline.Database.ExecuteSqlRawAsync("CREATE TABLE [__EFMigrationsHistory] ([MigrationId] nvarchar(150) NOT NULL PRIMARY KEY, [ProductVersion] nvarchar(32) NOT NULL); INSERT INTO [__EFMigrationsHistory] VALUES ('20260914072721_PerOrderChat', '9.0.0');");
        await using var upgraded = new OMSDbContext(options);
        var assembly = upgraded.GetService<IMigrationsAssembly>();
        var target = assembly.Migrations.Single(x => x.Key.EndsWith("_AddWooCommerceIntegration"));
        var migration = assembly.CreateMigration(target.Value, upgraded.Database.ProviderName!);
        if (migration.UpOperations.Any(op => op is not CreateTableOperation && op is not CreateIndexOperation))
            throw new Exception("Integration migration must be additive only.");
        var script = upgraded.GetService<IMigrator>().GenerateScript("20260914072721_PerOrderChat");
        await upgraded.Database.OpenConnectionAsync();
        try {
            foreach (var batch in Regex.Split(script, @"^GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
                if (!string.IsNullOrWhiteSpace(batch)) await upgraded.Database.ExecuteSqlRawAsync(batch);
        } finally { await upgraded.Database.CloseConnectionAsync(); }
        var preserved = await upgraded.Orders.SingleAsync();
        if (preserved.Amount != 750 || preserved.TrackingNumber != "EXISTING-TRACK" || preserved.ManufacturerOrderNumber != "AD1001" || preserved.CustomerProductTitle != "Existing production order")
            throw new Exception("Migration modified pre-existing order data.");
        if (await upgraded.Set<WooCommerceConnection>().CountAsync() != 0) throw new Exception("Unexpected connection after migration.");
        // Remove only this test fixture from the GUID-named disposable database.
        upgraded.Remove(preserved); await upgraded.SaveChangesAsync();
        upgraded.Remove(await upgraded.Users.SingleAsync()); await upgraded.SaveChangesAsync();
    }
    private sealed class BeforeIntegrationContext(DbContextOptions<OMSDbContext> options) : OMSDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder b)
        {
            base.OnModelCreating(b);
            b.Ignore<WooCommerceOrder>(); b.Ignore<WooCommerceCustomer>(); b.Ignore<WooCommerceSyncLog>();
            b.Ignore<WooCommerceAuthorization>(); b.Ignore<WooCommerceConnection>();
            b.Ignore<ShopifyStore>(); b.Ignore<ShopifyAuthorization>(); b.Ignore<ShopifyJob>(); b.Ignore<ShopifyShipment>();
        }
    }
}
