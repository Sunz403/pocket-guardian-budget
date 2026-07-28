using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AIShoppingAssistant.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Color = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Size = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ShippingCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    StoreName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FullName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastLogin = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Budgets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    MonthlyAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CurrentSpending = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Budgets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Budgets_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SearchHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    SearchTerm = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Budget = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SearchDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResultsCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SearchHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SearchHistories_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserPreferences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    FavoriteStyles = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FavoriteColors = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FavoriteStores = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PreferredPriceRangeMin = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PreferredPriceRangeMax = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPreferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserPreferences_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Products",
                columns: new[] { "Id", "Category", "Color", "CreatedAt", "Description", "ImageUrl", "Name", "Price", "ShippingCost", "Size", "StoreName" },
                values: new object[,]
                {
                    { 1, "Clothing", "White", new DateTime(2026, 7, 28, 0, 0, 0, 0, DateTimeKind.Utc), "Soft cotton everyday t-shirt.", "https://example.com/images/white-tshirt.jpg", "Classic White T-Shirt", 19.99m, 4.99m, "M", "StyleHub" },
                    { 2, "Clothing", "Blue", new DateTime(2026, 7, 28, 0, 0, 0, 0, DateTimeKind.Utc), "Denim jeans with a modern slim fit.", "https://example.com/images/slim-jeans.jpg", "Slim Fit Jeans", 49.99m, 6.99m, "32", "Denim Corner" },
                    { 3, "Shoes", "Black", new DateTime(2026, 7, 28, 0, 0, 0, 0, DateTimeKind.Utc), "Lightweight sneakers for daily runs.", "https://example.com/images/running-sneakers.jpg", "Running Sneakers", 89.50m, 8.99m, "9", "FastFeet" },
                    { 4, "Accessories", "Brown", new DateTime(2026, 7, 28, 0, 0, 0, 0, DateTimeKind.Utc), "Medium-sized handbag with zip closure.", "https://example.com/images/leather-handbag.jpg", "Leather Handbag", 120.00m, 10.50m, "Medium", "Urban Vogue" },
                    { 5, "Electronics", "Silver", new DateTime(2026, 7, 28, 0, 0, 0, 0, DateTimeKind.Utc), "Noise-reducing over-ear headphones.", "https://example.com/images/wireless-headphones.jpg", "Wireless Headphones", 159.99m, 12.99m, "One Size", "TechNest" },
                    { 6, "Activewear", "Purple", new DateTime(2026, 7, 28, 0, 0, 0, 0, DateTimeKind.Utc), "Stretch-fit leggings for workouts.", "https://example.com/images/yoga-leggings.jpg", "Yoga Leggings", 34.95m, 5.50m, "S", "ActiveLife" },
                    { 7, "Accessories", "Gray", new DateTime(2026, 7, 28, 0, 0, 0, 0, DateTimeKind.Utc), "Warm scarf for cooler weather.", "https://example.com/images/wool-scarf.jpg", "Wool Scarf", 24.99m, 3.99m, "One Size", "CozyWear" },
                    { 8, "Electronics", "Black", new DateTime(2026, 7, 28, 0, 0, 0, 0, DateTimeKind.Utc), "Fitness tracking smartwatch with notifications.", "https://example.com/images/smart-watch.jpg", "Smart Watch", 199.99m, 9.99m, "42mm", "TechNest" },
                    { 9, "Clothing", "Light Blue", new DateTime(2026, 7, 28, 0, 0, 0, 0, DateTimeKind.Utc), "Button-up shirt for office and events.", "https://example.com/images/formal-shirt.jpg", "Formal Shirt", 39.99m, 5.99m, "L", "StyleHub" },
                    { 10, "Bags", "Olive", new DateTime(2026, 7, 28, 0, 0, 0, 0, DateTimeKind.Utc), "Durable backpack for school or travel.", "https://example.com/images/canvas-backpack.jpg", "Canvas Backpack", 54.99m, 7.25m, "Large", "TrailLine" },
                    { 11, "Clothing", "Yellow", new DateTime(2026, 7, 28, 0, 0, 0, 0, DateTimeKind.Utc), "Light floral dress for warm days.", "https://example.com/images/summer-dress.jpg", "Summer Dress", 62.49m, 6.49m, "M", "Urban Vogue" },
                    { 12, "Accessories", "Black", new DateTime(2026, 7, 28, 0, 0, 0, 0, DateTimeKind.Utc), "Classic leather belt with metal buckle.", "https://example.com/images/leather-belt.jpg", "Leather Belt", 22.00m, 4.49m, "34", "Denim Corner" },
                    { 13, "Electronics", "Red", new DateTime(2026, 7, 28, 0, 0, 0, 0, DateTimeKind.Utc), "Ergonomic mouse with adjustable DPI.", "https://example.com/images/gaming-mouse.jpg", "Gaming Mouse", 45.75m, 5.99m, "One Size", "ClickZone" },
                    { 14, "Outerwear", "Navy", new DateTime(2026, 7, 28, 0, 0, 0, 0, DateTimeKind.Utc), "Insulated jacket for winter weather.", "https://example.com/images/puffer-jacket.jpg", "Puffer Jacket", 140.00m, 11.99m, "XL", "CozyWear" },
                    { 15, "Home Appliances", "White", new DateTime(2026, 7, 28, 0, 0, 0, 0, DateTimeKind.Utc), "High-speed blender for smoothies and soups.", "https://example.com/images/kitchen-blender.jpg", "Kitchen Blender", 79.99m, 13.50m, "1.5L", "HomeEase" },
                    { 16, "Accessories", "Green", new DateTime(2026, 7, 28, 0, 0, 0, 0, DateTimeKind.Utc), "Adjustable cap with breathable fabric.", "https://example.com/images/sports-cap.jpg", "Sports Cap", 18.50m, 3.75m, "One Size", "ActiveLife" },
                    { 17, "Shoes", "Tan", new DateTime(2026, 7, 28, 0, 0, 0, 0, DateTimeKind.Utc), "Stylish boots with side zipper.", "https://example.com/images/ankle-boots.jpg", "Ankle Boots", 95.00m, 8.50m, "8", "FastFeet" },
                    { 18, "Home Decor", "Silver", new DateTime(2026, 7, 28, 0, 0, 0, 0, DateTimeKind.Utc), "LED desk lamp with adjustable brightness.", "https://example.com/images/desk-lamp.jpg", "Desk Lamp", 31.20m, 6.25m, "Medium", "HomeEase" },
                    { 19, "Accessories", "Black", new DateTime(2026, 7, 28, 0, 0, 0, 0, DateTimeKind.Utc), "UV-protected sunglasses with slim frame.", "https://example.com/images/sunglasses.jpg", "Sunglasses", 27.99m, 4.20m, "One Size", "StyleHub" },
                    { 20, "Electronics", "Blue", new DateTime(2026, 7, 28, 0, 0, 0, 0, DateTimeKind.Utc), "Bluetooth speaker with rich sound.", "https://example.com/images/portable-speaker.jpg", "Portable Speaker", 68.80m, 7.80m, "Compact", "ClickZone" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Budgets_UserId_Month_Year",
                table: "Budgets",
                columns: new[] { "UserId", "Month", "Year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SearchHistories_UserId",
                table: "SearchHistories",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPreferences_UserId",
                table: "UserPreferences",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Budgets");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "SearchHistories");

            migrationBuilder.DropTable(
                name: "UserPreferences");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
