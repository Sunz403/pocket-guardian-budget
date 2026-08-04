using AIShoppingAssistant.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIShoppingAssistant.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260803153000_AddProductStoreUrl")]
public partial class AddProductStoreUrl : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "StoreUrl",
            table: "Products",
            type: "nvarchar(2048)",
            maxLength: 2048,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "StoreUrl", table: "Products");
    }
}
