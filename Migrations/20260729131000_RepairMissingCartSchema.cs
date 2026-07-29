using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIShoppingAssistant.Migrations;

[DbContext(typeof(Data.ApplicationDbContext))]
[Migration("20260729131000_RepairMissingCartSchema")]
public partial class RepairMissingCartSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF OBJECT_ID(N'[CartItems]', N'U') IS NULL
            BEGIN
                CREATE TABLE [CartItems] (
                    [Id] int NOT NULL IDENTITY,
                    [UserId] int NOT NULL,
                    [ProductId] int NOT NULL,
                    [ProductName] nvarchar(150) NOT NULL,
                    [Price] decimal(18,2) NOT NULL,
                    [Quantity] int NOT NULL,
                    [AddedDate] datetime2 NOT NULL,
                    CONSTRAINT [PK_CartItems] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_CartItems_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
                );

                CREATE UNIQUE INDEX [IX_CartItems_UserId_ProductId] ON [CartItems] ([UserId], [ProductId]);
            END
            """);

        migrationBuilder.Sql("""
            IF OBJECT_ID(N'[PurchaseHistories]', N'U') IS NULL
            BEGIN
                CREATE TABLE [PurchaseHistories] (
                    [Id] int NOT NULL IDENTITY,
                    [UserId] int NOT NULL,
                    [PurchaseDate] datetime2 NOT NULL,
                    [TotalAmount] decimal(18,2) NOT NULL,
                    [Items] nvarchar(max) NOT NULL,
                    CONSTRAINT [PK_PurchaseHistories] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_PurchaseHistories_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
                );

                CREATE INDEX [IX_PurchaseHistories_UserId] ON [PurchaseHistories] ([UserId]);
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
