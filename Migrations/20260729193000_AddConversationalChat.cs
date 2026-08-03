using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIShoppingAssistant.Migrations;

[Migration("20260729193000_AddConversationalChat")]
public partial class AddConversationalChat : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ChatSessions",
            columns: table => new
            {
                Id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                UserId = table.Column<int>(type: "int", nullable: false),
                StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                EndedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ChatSessions", x => x.Id);
                table.ForeignKey("FK_ChatSessions_Users_UserId", x => x.UserId, "Users", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ChatMessages",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                UserId = table.Column<int>(type: "int", nullable: false),
                Message = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                Sender = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                ChatSessionId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ChatMessages", x => x.Id);
                table.ForeignKey("FK_ChatMessages_ChatSessions_ChatSessionId", x => x.ChatSessionId, "ChatSessions", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_ChatMessages_Users_UserId", x => x.UserId, "Users", "Id");
            });

        migrationBuilder.CreateIndex(name: "IX_ChatSessions_UserId_EndedAt_StartedAt", table: "ChatSessions", columns: new[] { "UserId", "EndedAt", "StartedAt" });
        migrationBuilder.CreateIndex(name: "IX_ChatMessages_ChatSessionId_Timestamp", table: "ChatMessages", columns: new[] { "ChatSessionId", "Timestamp" });
        migrationBuilder.CreateIndex(name: "IX_ChatMessages_UserId_Timestamp", table: "ChatMessages", columns: new[] { "UserId", "Timestamp" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ChatMessages");
        migrationBuilder.DropTable(name: "ChatSessions");
    }
}
