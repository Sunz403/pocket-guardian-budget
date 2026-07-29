using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIShoppingAssistant.Migrations;

[DbContext(typeof(Data.ApplicationDbContext))]
[Migration("20260728170000_AddBudgetAlertToUserPreferences")]
public partial class AddBudgetAlertToUserPreferences : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "BudgetAlertEnabled",
            table: "UserPreferences",
            type: "bit",
            nullable: false,
            defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "BudgetAlertEnabled",
            table: "UserPreferences");
    }
}
