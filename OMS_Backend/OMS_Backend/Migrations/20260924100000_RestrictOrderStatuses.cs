using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OMS_Backend.Data;

namespace OMS_Backend.Migrations;

[DbContext(typeof(OMSDbContext))]
[Migration("20260924100000_RestrictOrderStatuses")]
public class RestrictOrderStatuses : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // IDs are installation-specific. Preserve old rows for existing orders/history.
        migrationBuilder.Sql("""
            DECLARE @Names TABLE (Name nvarchar(100) PRIMARY KEY);
            INSERT INTO @Names VALUES (N'Assign'), (N'In Manufacturing'), (N'Refund'), (N'Cancel');
            INSERT INTO LookupItems (LookupDataTypeId, Name, IsActive, IsDeleted, CreatedDate, CreatedBy)
            SELECT 1, n.Name, 1, 0, SYSUTCDATETIME(), 0
            FROM @Names n
            WHERE NOT EXISTS (SELECT 1 FROM LookupItems li WHERE li.LookupDataTypeId = 1
                AND LOWER(LTRIM(RTRIM(li.Name))) = LOWER(n.Name));

            DECLARE @Canonical TABLE (Id int PRIMARY KEY, Name nvarchar(100));
            INSERT INTO @Canonical
            SELECT MIN(li.Id), n.Name FROM @Names n JOIN LookupItems li
                ON li.LookupDataTypeId = 1 AND LOWER(LTRIM(RTRIM(li.Name))) = LOWER(n.Name)
            GROUP BY n.Name;
            UPDATE li SET Name = c.Name, IsActive = 1, IsDeleted = 0
            FROM LookupItems li JOIN @Canonical c ON c.Id = li.Id;
            UPDATE LookupItems SET IsActive = 0
            WHERE LookupDataTypeId = 1 AND Id NOT IN (SELECT Id FROM @Canonical);

            DECLARE @AssignId int = (SELECT Id FROM @Canonical WHERE Name = N'Assign');
            -- Reset existing orders as requested; retain their audit trail.
            INSERT INTO OrderStatusHistories (OrderId, StatusId, IsActive, IsDeleted, CreatedDate, CreatedBy)
            SELECT Id, @AssignId, 1, 0, SYSUTCDATETIME(), 0
            FROM Orders WHERE OrderStatusId <> @AssignId;
            UPDATE Orders SET OrderStatusId = @AssignId, UpdatedDate = SYSUTCDATETIME(), UpdatedBy = 0
            WHERE OrderStatusId <> @AssignId;
            -- Keep future imports valid.
            UPDATE WooCommerceConnection SET DefaultStatusId = @AssignId
            WHERE DefaultStatusId NOT IN (SELECT Id FROM @Canonical);
            UPDATE ShopifyAuthorization SET StatusId = @AssignId
            WHERE StatusId NOT IN (SELECT Id FROM @Canonical);
            -- A retired shipped status must never be remapped to an earlier workflow stage.
            UPDATE ShopifyStore SET FulfillmentEnabled = 0, ShippedStatusId = NULL
            WHERE ShippedStatusId IS NOT NULL AND ShippedStatusId NOT IN (SELECT Id FROM @Canonical);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        throw new NotSupportedException("Restore the database backup to roll back the status catalog; new statuses may already be referenced by orders and history.");
    }
}
