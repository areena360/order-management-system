using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OMS_Backend.Migrations
{
    /// <inheritdoc />
    public partial class ManufacturingTimeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ManufacturingEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    RoleName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsSnapshot = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManufacturingEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ManufacturingEvents_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ManufacturingEvents_OrderId_OccurredAt",
                table: "ManufacturingEvents",
                columns: new[] { "OrderId", "OccurredAt" });
            // Only assignment time was previously retained. Preserve it, and explicitly
            // label existing non-assigned progress as a snapshot rather than inventing dates.
            migrationBuilder.Sql("""
                INSERT INTO ManufacturingEvents (OrderId, UserId, UserName, RoleId, RoleName, Status, OccurredAt, IsSnapshot)
                SELECT a.OrderId, a.UserId, CONCAT(u.FirstName, ' ', u.LastName), a.RoleId, r.Name, 'assigned', a.AssignedAt, 0
                FROM AdminOrderAssignments a JOIN Users u ON u.Id = a.UserId JOIN Roles r ON r.Id = a.RoleId;
                INSERT INTO ManufacturingEvents (OrderId, UserId, UserName, RoleId, RoleName, Status, OccurredAt, IsSnapshot)
                SELECT a.OrderId, a.UserId, CONCAT(u.FirstName, ' ', u.LastName), a.RoleId, r.Name, a.Status, SYSUTCDATETIME(), 1
                FROM AdminOrderAssignments a JOIN Users u ON u.Id = a.UserId JOIN Roles r ON r.Id = a.RoleId
                WHERE a.Status <> 'assigned';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ManufacturingEvents");
        }
    }
}
