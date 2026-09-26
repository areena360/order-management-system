using Microsoft.EntityFrameworkCore;
using OMS_Backend.Models;

namespace OMS_Backend.Data
{
    public class OMSDbContext : DbContext
    {
        public OMSDbContext(DbContextOptions<OMSDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<AuthSession> AuthSessions { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderImage> OrderImages { get; set; }
        public DbSet<OrderStatusHistory> OrderStatusHistories { get; set; }
        public DbSet<InventoryBill> InventoryBills { get; set; }
        public DbSet<LookupType> LookupTypes { get; set; }
        public DbSet<LookupItem> LookupItems { get; set; }
        public DbSet<PasswordResetToken> PasswordResetTokens { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }
        public DbSet<ChatReadState> ChatReadStates { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            WooCommerceModel.Configure(modelBuilder);
            ShopifyModel.Configure(modelBuilder);

            modelBuilder.Entity<ChatReadState>().HasKey(r => new { r.UserId, r.OrderId, r.Channel });
            modelBuilder.Entity<ChatReadState>().Property(r => r.Channel).HasMaxLength(20);

            modelBuilder.Entity<Order>().Ignore(o => o.CreatedDate);
            modelBuilder.Entity<Order>().Ignore(o => o.UpdatedDate);

            modelBuilder.Entity<AuthSession>().HasOne(s => s.User).WithMany()
                .HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<AuthSession>().HasIndex(s => s.ExpiresAt);

            // ---------------- ChatMessage ----------------
            modelBuilder.Entity<ChatMessage>()
                .HasOne(m => m.Order).WithMany()
                .HasForeignKey(m => m.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ChatMessage>()
                .HasOne(m => m.Customer).WithMany()
                .HasForeignKey(m => m.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ChatMessage>()
                .HasOne(m => m.Sender).WithMany()
                .HasForeignKey(m => m.SenderUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ChatMessage>()
                .HasIndex(m => new { m.OrderId, m.CreatedDate });

            modelBuilder.Entity<ChatMessage>()
                .Property(m => m.Channel)
                .HasMaxLength(20)
                .HasDefaultValue("Customer");

            modelBuilder.Entity<ChatMessage>()
                .HasIndex(m => new { m.OrderId, m.Channel, m.CreatedDate });

            modelBuilder.Entity<OrderImage>().HasQueryFilter(x => !x.IsDeleted);
            modelBuilder.Entity<InventoryBill>().HasQueryFilter(x => !x.IsDeleted);

            // =====================================================
            // ✅ FIXED: BaseEntity CreatedDate default
            // =====================================================
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
                {
                    if (entityType.ClrType != typeof(Order))
                        modelBuilder.Entity(entityType.ClrType)
                            .Property<DateTime>(nameof(BaseEntity.CreatedDate))
                            .HasDefaultValueSql("GETUTCDATE()");
                }
            }

            // =====================================================
            // ✅ FIXED: RolePermission config — loop ke BAAHAR
            // =====================================================
            modelBuilder.Entity<RolePermission>()
                .HasOne(rp => rp.Role).WithMany()
                .HasForeignKey(rp => rp.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<RolePermission>()
                .HasIndex(rp => new { rp.RoleId, rp.ScreenKey })
                .IsUnique();

            // =====================================================
            // ✅ NEW: Role.Name unique — duplicate roles rokne ke liye
            // =====================================================
            modelBuilder.Entity<Role>()
                .HasIndex(r => r.Name)
                .IsUnique();

            // =====================================================
            // ✅ NEW: Role filter (soft-delete aware) — optional but recommended
            // =====================================================
            modelBuilder.Entity<Role>().HasQueryFilter(r => !r.IsDeleted);

            modelBuilder.Entity<Order>().HasIndex(o => new { o.IsAssigned, o.AssignedDate });

            modelBuilder.Entity<PasswordResetToken>()
                .HasOne(t => t.User).WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<PasswordResetToken>().HasIndex(t => t.Token);

            modelBuilder.Entity<User>()
                .HasOne(u => u.Role).WithMany(r => r.Users)
                .HasForeignKey(u => u.RoleId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Order>()
                .HasOne(o => o.Customer).WithMany()
                .HasForeignKey(o => o.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<OrderImage>()
                .HasOne(oi => oi.Order).WithMany(o => o.OrderImages)
                .HasForeignKey(oi => oi.OrderId);

            modelBuilder.Entity<OrderStatusHistory>()
                .HasOne(h => h.Order).WithMany(o => o.StatusHistories)
                .HasForeignKey(h => h.OrderId);

            modelBuilder.Entity<InventoryBill>()
                .HasOne(b => b.Order).WithMany(o => o.InventoryBills)
                .HasForeignKey(b => b.OrderId);

            modelBuilder.Entity<LookupItem>()
                .HasOne(li => li.LookupType).WithMany(lt => lt.LookupItems)
                .HasForeignKey(li => li.LookupDataTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            // ---------------- Roles seed ----------------
            modelBuilder.Entity<Role>().HasData(
                new Role { Id = 1, Name = "Super Admin", IsActive = true, IsDeleted = false },
                new Role { Id = 2, Name = "Admin", IsActive = true, IsDeleted = false },
                new Role { Id = 3, Name = "Finance", IsActive = true, IsDeleted = false },
                new Role { Id = 4, Name = "Customer", IsActive = true, IsDeleted = false },
                new Role { Id = 5, Name = "Staff", IsActive = true, IsDeleted = false },
                new Role { Id = 6, Name = "Sales", IsActive = true, IsDeleted = false }
            );

            // ---------------- Lookups ----------------
            modelBuilder.Entity<LookupType>().HasData(
                new LookupType { Id = 1, Name = "OrderStatus", IsActive = true, IsDeleted = false },
                new LookupType { Id = 2, Name = "Priority", IsActive = true, IsDeleted = false },
                new LookupType { Id = 3, Name = "Gender", IsActive = true, IsDeleted = false },
                new LookupType { Id = 4, Name = "Material", IsActive = true, IsDeleted = false },
                new LookupType { Id = 5, Name = "Size", IsActive = true, IsDeleted = false },
                new LookupType { Id = 6, Name = "SizeChart", IsActive = true, IsDeleted = false }
            );

            modelBuilder.Entity<LookupItem>().HasData(
                new LookupItem { Id = 1, LookupDataTypeId = 2, Name = "Most Urgent", IsActive = true, IsDeleted = false },
                new LookupItem { Id = 2, LookupDataTypeId = 2, Name = "PayPal Dispute", IsActive = true, IsDeleted = false },
                new LookupItem { Id = 3, LookupDataTypeId = 3, Name = "Male", IsActive = true, IsDeleted = false },
                new LookupItem { Id = 4, LookupDataTypeId = 3, Name = "Female", IsActive = true, IsDeleted = false },
                new LookupItem { Id = 5, LookupDataTypeId = 3, Name = "Unisex", IsActive = true, IsDeleted = false },
                new LookupItem { Id = 6, LookupDataTypeId = 4, Name = "CowLeather", IsActive = true, IsDeleted = false },
                new LookupItem { Id = 7, LookupDataTypeId = 4, Name = "SheepLeather", IsActive = true, IsDeleted = false },
                new LookupItem { Id = 8, LookupDataTypeId = 4, Name = "SuedeLeather", IsActive = true, IsDeleted = false },
                new LookupItem { Id = 9, LookupDataTypeId = 4, Name = "FauxLeather", IsActive = true, IsDeleted = false },
                new LookupItem { Id = 10, LookupDataTypeId = 4, Name = "Fleece", IsActive = true, IsDeleted = false },
                new LookupItem { Id = 11, LookupDataTypeId = 4, Name = "Fur", IsActive = true, IsDeleted = false },
                new LookupItem { Id = 12, LookupDataTypeId = 4, Name = "Cotton", IsActive = true, IsDeleted = false },
                new LookupItem { Id = 13, LookupDataTypeId = 4, Name = "Silk", IsActive = true, IsDeleted = false },
                new LookupItem { Id = 14, LookupDataTypeId = 4, Name = "Wool", IsActive = true, IsDeleted = false },
                new LookupItem { Id = 15, LookupDataTypeId = 4, Name = "Satin", IsActive = true, IsDeleted = false },
                new LookupItem { Id = 16, LookupDataTypeId = 4, Name = "Parachute", IsActive = true, IsDeleted = false },
                new LookupItem { Id = 17, LookupDataTypeId = 4, Name = "Sequin", IsActive = true, IsDeleted = false },
                new LookupItem { Id = 18, LookupDataTypeId = 4, Name = "Polyester", IsActive = true, IsDeleted = false },
                new LookupItem { Id = 19, LookupDataTypeId = 4, Name = "Denim", IsActive = true, IsDeleted = false },
                new LookupItem { Id = 20, LookupDataTypeId = 4, Name = "Velvet", IsActive = true, IsDeleted = false },
                new LookupItem { Id = 21, LookupDataTypeId = 4, Name = "Jersey", IsActive = true, IsDeleted = false },
                new LookupItem { Id = 22, LookupDataTypeId = 4, Name = "Net", IsActive = true, IsDeleted = false },
                new LookupItem { Id = 23, LookupDataTypeId = 5, Name = "XXS", IsActive = true, IsDeleted = false },
                new LookupItem { Id = 24, LookupDataTypeId = 5, Name = "XS", IsActive = true, IsDeleted = false },
                new LookupItem { Id = 25, LookupDataTypeId = 5, Name = "S", IsActive = true, IsDeleted = false },
                new LookupItem { Id = 26, LookupDataTypeId = 5, Name = "M", IsActive = true, IsDeleted = false },
                new LookupItem { Id = 27, LookupDataTypeId = 5, Name = "L", IsActive = true, IsDeleted = false },
                new LookupItem { Id = 28, LookupDataTypeId = 5, Name = "XL", IsActive = true, IsDeleted = false },
                new LookupItem { Id = 29, LookupDataTypeId = 5, Name = "XXL", IsActive = true, IsDeleted = false },
                new LookupItem { Id = 30, LookupDataTypeId = 5, Name = "XXXL", IsActive = true, IsDeleted = false },
                new LookupItem { Id = 31, LookupDataTypeId = 5, Name = "XXXXL", IsActive = true, IsDeleted = false },
                new LookupItem { Id = 32, LookupDataTypeId = 5, Name = "XXXXXL", IsActive = true, IsDeleted = false },
                new LookupItem { Id = 33, LookupDataTypeId = 6, Name = "UK/US Chart", IsActive = true, IsDeleted = false },
                new LookupItem { Id = 34, LookupDataTypeId = 6, Name = "Pak Chart", IsActive = true, IsDeleted = false }
            );
        }
    }
}