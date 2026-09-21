using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OMS_Backend.Migrations
{
    /// <inheritdoc />
    public partial class WooCommerceQuantityUnits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WooCommerceOrder_ConnectionId_ExternalOrderId",
                table: "WooCommerceOrder");

            migrationBuilder.AddColumn<long>(
                name: "ExternalLineId",
                table: "WooCommerceOrder",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<bool>(
                name: "IsCurrentUnit",
                table: "WooCommerceOrder",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "UnitNumber",
                table: "WooCommerceOrder",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_WooCommerceOrder_ConnectionId_ExternalOrderId_ExternalLineId_UnitNumber",
                table: "WooCommerceOrder",
                columns: new[] { "ConnectionId", "ExternalOrderId", "ExternalLineId", "UnitNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WooCommerceOrder_ConnectionId_ExternalOrderId_ExternalLineId_UnitNumber",
                table: "WooCommerceOrder");

            migrationBuilder.DropColumn(
                name: "ExternalLineId",
                table: "WooCommerceOrder");

            migrationBuilder.DropColumn(
                name: "IsCurrentUnit",
                table: "WooCommerceOrder");

            migrationBuilder.DropColumn(
                name: "UnitNumber",
                table: "WooCommerceOrder");

            migrationBuilder.CreateIndex(
                name: "IX_WooCommerceOrder_ConnectionId_ExternalOrderId",
                table: "WooCommerceOrder",
                columns: new[] { "ConnectionId", "ExternalOrderId" },
                unique: true);
        }
    }
}

