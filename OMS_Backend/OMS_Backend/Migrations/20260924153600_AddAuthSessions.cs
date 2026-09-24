using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OMS_Backend.Data;

namespace OMS_Backend.Migrations;

[DbContext(typeof(OMSDbContext))]
[Migration("20260924153600_AddAuthSessions")]
public class AddAuthSessions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AuthSessions",
            columns: table => new
            {
                TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                UserId = table.Column<int>(type: "int", nullable: false),
                PasswordVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                RememberMe = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AuthSessions", x => x.TokenHash);
                table.ForeignKey(
                    name: "FK_AuthSessions_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AuthSessions_ExpiresAt",
            table: "AuthSessions",
            column: "ExpiresAt");

        migrationBuilder.CreateIndex(
            name: "IX_AuthSessions_UserId",
            table: "AuthSessions",
            column: "UserId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "AuthSessions");
    }
}
