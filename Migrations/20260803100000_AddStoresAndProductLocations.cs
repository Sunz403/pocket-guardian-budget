using AIShoppingAssistant.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIShoppingAssistant.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260803100000_AddStoresAndProductLocations")]
public partial class AddStoresAndProductLocations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Stores",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                Address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                Latitude = table.Column<double>(type: "float", nullable: false),
                Longitude = table.Column<double>(type: "float", nullable: false),
                Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Stores", x => x.Id));

        migrationBuilder.AddColumn<int>(name: "StoreId", table: "Products", type: "int", nullable: true);
        migrationBuilder.CreateIndex(name: "IX_Stores_Name", table: "Stores", column: "Name", unique: true);
        migrationBuilder.CreateIndex(name: "IX_Products_StoreId", table: "Products", column: "StoreId");
        migrationBuilder.AddForeignKey(
            name: "FK_Products_Stores_StoreId",
            table: "Products",
            column: "StoreId",
            principalTable: "Stores",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(name: "FK_Products_Stores_StoreId", table: "Products");
        migrationBuilder.DropIndex(name: "IX_Products_StoreId", table: "Products");
        migrationBuilder.DropTable(name: "Stores");
        migrationBuilder.DropColumn(name: "StoreId", table: "Products");
    }
}
