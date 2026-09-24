using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OMS_Backend.Migrations
{
    /// <inheritdoc />
    public partial class OrderAssignmentVisibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedDate",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "UpdatedDate",
                table: "Orders");

            migrationBuilder.AddColumn<bool>(
                name: "RequiresCustomerAssignment",
                table: "Orders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Imported orders and customer/unknown-origin orders require customer release.
            // Only orders with a known staff creator remain visible before assignment.
            migrationBuilder.Sql("""
                UPDATE o SET RequiresCustomerAssignment = CASE
                    WHEN EXISTS (SELECT 1 FROM Users u WHERE u.Id = o.CreatedBy AND u.RoleId IN (1,2,3,5,6))
                        AND NOT EXISTS (SELECT 1 FROM WooCommerceOrder w WHERE w.OrderId = o.Id)
                    THEN 0 ELSE 1 END
                FROM Orders o;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_IsAssigned_AssignedDate",
                table: "Orders",
                columns: new[] { "IsAssigned", "AssignedDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_IsAssigned_AssignedDate",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "RequiresCustomerAssignment",
                table: "Orders");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedDate",
                table: "Orders",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedDate",
                table: "Orders",
                type: "datetime2",
                nullable: true);
        }
    }
}
