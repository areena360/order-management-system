using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OMS_Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddShopifyIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Provider",
                table: "WooCommerceConnection",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "WooCommerce");

            migrationBuilder.CreateTable(
                name: "ShopifyAuthorization",
                columns: table => new
                {
                    StateHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Shop = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    OwnerUserId = table.Column<int>(type: "int", nullable: false),
                    GenderId = table.Column<int>(type: "int", nullable: false),
                    MaterialId = table.Column<int>(type: "int", nullable: false),
                    StatusId = table.Column<int>(type: "int", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Used = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShopifyAuthorization", x => x.StateHash);
                });

            migrationBuilder.CreateTable(
                name: "ShopifyStore",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Shop = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ConnectionId = table.Column<int>(type: "int", nullable: false),
                    ProtectedToken = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProtectedRefreshToken = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TokenExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Scopes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NextPollAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ScanSince = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ScanUntil = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ScanCursor = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FulfillmentEnabled = table.Column<bool>(type: "bit", nullable: false),
                    ShippedStatusId = table.Column<int>(type: "int", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShopifyStore", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShopifyStore_WooCommerceConnection_ConnectionId",
                        column: x => x.ConnectionId,
                        principalTable: "WooCommerceConnection",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ShopifyJob",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    EventId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Topic = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ExternalId = table.Column<long>(type: "bigint", nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    DueAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShopifyJob", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShopifyJob_ShopifyStore_StoreId",
                        column: x => x.StoreId,
                        principalTable: "ShopifyStore",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ShopifyShipment",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    ExternalOrderId = table.Column<long>(type: "bigint", nullable: false),
                    FulfillmentIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TrackingHash = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShopifyShipment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShopifyShipment_ShopifyStore_StoreId",
                        column: x => x.StoreId,
                        principalTable: "ShopifyStore",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShopifyAuthorization_ExpiresAt",
                table: "ShopifyAuthorization",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_ShopifyJob_CompletedAt_DueAt",
                table: "ShopifyJob",
                columns: new[] { "CompletedAt", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ShopifyJob_StoreId_EventId",
                table: "ShopifyJob",
                columns: new[] { "StoreId", "EventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShopifyShipment_StoreId_ExternalOrderId",
                table: "ShopifyShipment",
                columns: new[] { "StoreId", "ExternalOrderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShopifyStore_ConnectionId",
                table: "ShopifyStore",
                column: "ConnectionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShopifyStore_Shop",
                table: "ShopifyStore",
                column: "Shop",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShopifyAuthorization");

            migrationBuilder.DropTable(
                name: "ShopifyJob");

            migrationBuilder.DropTable(
                name: "ShopifyShipment");

            migrationBuilder.DropTable(
                name: "ShopifyStore");

            migrationBuilder.DropColumn(
                name: "Provider",
                table: "WooCommerceConnection");
        }
    }
}
