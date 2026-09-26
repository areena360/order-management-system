using Microsoft.EntityFrameworkCore.Migrations;
namespace OMS_Backend.Migrations;
public partial class AddChatReadStates : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
            migrationBuilder.CreateTable(
                name: "ChatReadStates",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    LastReadMessageId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatReadStates", x => new { x.UserId, x.OrderId, x.Channel });
                });
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ChatReadStates");
    }
}
