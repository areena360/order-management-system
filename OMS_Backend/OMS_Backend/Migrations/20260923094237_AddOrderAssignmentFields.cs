using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OMS_Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderAssignmentFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AssignedDate",
                table: "Orders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAssigned",
                table: "Orders",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssignedDate",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "IsAssigned",
                table: "Orders");
        }
    }
}
