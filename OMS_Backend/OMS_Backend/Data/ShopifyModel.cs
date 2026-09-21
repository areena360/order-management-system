using Microsoft.EntityFrameworkCore;
using OMS_Backend.Models;
namespace OMS_Backend.Data;
public static class ShopifyModel {
    public static void Configure(ModelBuilder b) {
        b.Entity<ShopifyStore>().HasIndex(x=>x.Shop).IsUnique();
        b.Entity<ShopifyStore>().HasIndex(x=>x.ConnectionId).IsUnique();
        b.Entity<ShopifyStore>().HasOne(x=>x.Connection).WithMany().HasForeignKey(x=>x.ConnectionId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ShopifyAuthorization>().HasIndex(x=>x.ExpiresAt);
        b.Entity<ShopifyJob>().HasIndex(x=>new{x.StoreId,x.EventId}).IsUnique();
        b.Entity<ShopifyJob>().HasIndex(x=>new{x.CompletedAt,x.DueAt});
        b.Entity<ShopifyJob>().HasOne(x=>x.Store).WithMany().HasForeignKey(x=>x.StoreId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ShopifyShipment>().HasIndex(x=>new{x.StoreId,x.ExternalOrderId}).IsUnique();
        b.Entity<ShopifyShipment>().HasOne(x=>x.Store).WithMany().HasForeignKey(x=>x.StoreId).OnDelete(DeleteBehavior.Restrict);
        var utc = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime,DateTime>(x=>x,x=>DateTime.SpecifyKind(x,DateTimeKind.Utc));
        foreach(var entity in b.Model.GetEntityTypes().Where(x=>x.ClrType.Name.StartsWith("Shopify")))
            foreach(var property in entity.GetProperties().Where(x=>x.ClrType==typeof(DateTime)||x.ClrType==typeof(DateTime?))) property.SetValueConverter(utc);
    }
}
