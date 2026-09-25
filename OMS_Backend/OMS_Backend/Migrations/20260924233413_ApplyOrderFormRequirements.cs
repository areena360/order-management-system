using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OMS_Backend.Migrations
{
    /// <inheritdoc />
    public partial class ApplyOrderFormRequirements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "ManufacturerMaterialId",
                table: "Orders",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "CustomerMaterialId",
                table: "Orders",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "Courier",
                table: "Orders",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippingContact",
                table: "Orders",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippingEmail",
                table: "Orders",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.Sql("""
                DECLARE @Status TABLE (Name nvarchar(100) PRIMARY KEY);
                INSERT INTO @Status VALUES (N'new'), (N'assign'), (N'in manufacturing'), (N'completed'), (N'shipped'), (N'refund'), (N'cancel');
                INSERT INTO LookupItems (LookupDataTypeId, Name, IsActive, IsDeleted, CreatedDate, CreatedBy)
                SELECT 1, s.Name, 1, 0, SYSUTCDATETIME(), 0 FROM @Status s
                WHERE NOT EXISTS (SELECT 1 FROM LookupItems l WHERE l.LookupDataTypeId = 1 AND LOWER(LTRIM(RTRIM(l.Name))) = s.Name);
                UPDATE l SET Name = s.Name, IsActive = 1, IsDeleted = 0
                FROM LookupItems l JOIN @Status s ON l.LookupDataTypeId = 1 AND LOWER(LTRIM(RTRIM(l.Name))) = s.Name;

                DECLARE @Priority TABLE (Name nvarchar(100) PRIMARY KEY);
                INSERT INTO @Priority VALUES (N'urgent'), (N'most urgent'), (N'normal'), (N'low');
                INSERT INTO LookupItems (LookupDataTypeId, Name, IsActive, IsDeleted, CreatedDate, CreatedBy)
                SELECT 2, p.Name, 1, 0, SYSUTCDATETIME(), 0 FROM @Priority p
                WHERE NOT EXISTS (SELECT 1 FROM LookupItems l WHERE l.LookupDataTypeId = 2 AND LOWER(LTRIM(RTRIM(l.Name))) = p.Name);
                UPDATE l SET Name = p.Name, IsActive = 1, IsDeleted = 0
                FROM LookupItems l JOIN @Priority p ON l.LookupDataTypeId = 2 AND LOWER(LTRIM(RTRIM(l.Name))) = p.Name;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Courier",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ShippingContact",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ShippingEmail",
                table: "Orders");

            migrationBuilder.AlterColumn<int>(
                name: "ManufacturerMaterialId",
                table: "Orders",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "CustomerMaterialId",
                table: "Orders",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
