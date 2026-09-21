using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OMS_Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddWooCommerceIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WooCommerceAuthorization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CodeHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    InstallationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StoreName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StoreUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PluginVersion = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConnectionId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WooCommerceAuthorization", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WooCommerceConnection",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnerUserId = table.Column<int>(type: "int", nullable: false),
                    InstallationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StoreName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StoreUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    AccessTokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PluginVersion = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSyncAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DefaultGenderId = table.Column<int>(type: "int", nullable: false),
                    DefaultMaterialId = table.Column<int>(type: "int", nullable: false),
                    DefaultStatusId = table.Column<int>(type: "int", nullable: false),
                    StatusMappingsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SyncRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WooCommerceConnection", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WooCommerceConnection_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WooCommerceCustomer",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConnectionId = table.Column<int>(type: "int", nullable: false),
                    CustomerKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ExternalCustomerId = table.Column<long>(type: "bigint", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WooCommerceCustomer", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WooCommerceCustomer_WooCommerceConnection_ConnectionId",
                        column: x => x.ConnectionId,
                        principalTable: "WooCommerceConnection",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WooCommerceSyncLog",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConnectionId = table.Column<int>(type: "int", nullable: false),
                    ExternalOrderId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Result = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WooCommerceSyncLog", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WooCommerceSyncLog_WooCommerceConnection_ConnectionId",
                        column: x => x.ConnectionId,
                        principalTable: "WooCommerceConnection",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WooCommerceOrder",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConnectionId = table.Column<int>(type: "int", nullable: false),
                    ExternalOrderId = table.Column<long>(type: "bigint", nullable: false),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    BuyerId = table.Column<int>(type: "int", nullable: false),
                    ExternalStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PayloadHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ItemsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BillingJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ShippingJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PaymentMethod = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WooCommerceOrder", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WooCommerceOrder_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WooCommerceOrder_WooCommerceConnection_ConnectionId",
                        column: x => x.ConnectionId,
                        principalTable: "WooCommerceConnection",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WooCommerceOrder_WooCommerceCustomer_BuyerId",
                        column: x => x.BuyerId,
                        principalTable: "WooCommerceCustomer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WooCommerceAuthorization_CodeHash",
                table: "WooCommerceAuthorization",
                column: "CodeHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WooCommerceAuthorization_ExpiresAt",
                table: "WooCommerceAuthorization",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_WooCommerceConnection_InstallationId",
                table: "WooCommerceConnection",
                column: "InstallationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WooCommerceConnection_OwnerUserId",
                table: "WooCommerceConnection",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WooCommerceCustomer_ConnectionId_CustomerKey",
                table: "WooCommerceCustomer",
                columns: new[] { "ConnectionId", "CustomerKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WooCommerceOrder_BuyerId",
                table: "WooCommerceOrder",
                column: "BuyerId");

            migrationBuilder.CreateIndex(
                name: "IX_WooCommerceOrder_ConnectionId_ExternalOrderId",
                table: "WooCommerceOrder",
                columns: new[] { "ConnectionId", "ExternalOrderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WooCommerceOrder_OrderId",
                table: "WooCommerceOrder",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WooCommerceSyncLog_ConnectionId_CreatedDate",
                table: "WooCommerceSyncLog",
                columns: new[] { "ConnectionId", "CreatedDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WooCommerceAuthorization");

            migrationBuilder.DropTable(
                name: "WooCommerceOrder");

            migrationBuilder.DropTable(
                name: "WooCommerceSyncLog");

            migrationBuilder.DropTable(
                name: "WooCommerceCustomer");

            migrationBuilder.DropTable(
                name: "WooCommerceConnection");
        }
    }
}
