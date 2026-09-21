using Microsoft.EntityFrameworkCore;
using OMS_Backend.Models;

namespace OMS_Backend.Data;

public static class WooCommerceModel
{
    public static void Configure(ModelBuilder b)
    {
        b.Entity<WooCommerceConnection>().HasIndex(x => x.InstallationId).IsUnique();
        b.Entity<WooCommerceConnection>().Property(x=>x.Provider).HasDefaultValue("WooCommerce");
        b.Entity<WooCommerceConnection>().HasOne(x => x.OwnerUser).WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<WooCommerceAuthorization>().HasIndex(x => x.CodeHash).IsUnique();
        b.Entity<WooCommerceAuthorization>().HasIndex(x => x.ExpiresAt);
        b.Entity<WooCommerceCustomer>().HasIndex(x => new { x.ConnectionId, x.CustomerKey }).IsUnique();
        b.Entity<WooCommerceCustomer>().HasOne(x => x.Connection).WithMany().HasForeignKey(x => x.ConnectionId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<WooCommerceOrder>().HasIndex(x => new { x.ConnectionId, x.ExternalOrderId, x.ExternalLineId, x.UnitNumber }).IsUnique();
        b.Entity<WooCommerceOrder>().HasIndex(x => x.OrderId).IsUnique();
        b.Entity<WooCommerceOrder>().Property(x => x.Total).HasPrecision(18, 4);
        b.Entity<WooCommerceOrder>().HasOne(x => x.Connection).WithMany().HasForeignKey(x => x.ConnectionId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<WooCommerceOrder>().HasOne(x => x.Order).WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<WooCommerceOrder>().HasOne(x => x.Buyer).WithMany().HasForeignKey(x => x.BuyerId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<WooCommerceSyncLog>().HasOne(x => x.Connection).WithMany().HasForeignKey(x => x.ConnectionId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<WooCommerceSyncLog>().HasIndex(x => new { x.ConnectionId, x.CreatedDate });
        // SQL Server datetime2 has no timezone; restore UTC on materialization for API consumers.
        var utc = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime, DateTime>(
            value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
        foreach (var entity in b.Model.GetEntityTypes().Where(x => x.ClrType.Name.StartsWith("WooCommerce")))
            foreach (var property in entity.GetProperties().Where(x => x.ClrType == typeof(DateTime) || x.ClrType == typeof(DateTime?)))
                property.SetValueConverter(utc);
    }
}
