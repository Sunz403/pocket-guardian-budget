using AIShoppingAssistant.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIShoppingAssistant.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260729180000_AddProductImageUpload")]
public partial class AddProductImageUpload : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ImageFileName",
            table: "Products",
            type: "nvarchar(255)",
            maxLength: 255,
            nullable: true);

        migrationBuilder.DropColumn(
            name: "ImageUrl",
            table: "Products");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ImageUrl",
            table: "Products",
            type: "nvarchar(500)",
            maxLength: 500,
            nullable: true);

        migrationBuilder.DropColumn(
            name: "ImageFileName",
            table: "Products");
    }
}
