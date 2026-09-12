using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OMS_Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddSoftDeleteToOrderImagesAndInventoryBills : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "OrderImages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "InventoryBills",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "OrderImages");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "InventoryBills");
        }
    }
}