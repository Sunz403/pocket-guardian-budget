using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIShoppingAssistant.Migrations;

// Repairs databases whose migration history includes AddConversationalChat but
// whose chat tables were removed or were never created.
[Migration("20260731120000_RepairMissingChatSchema")]
public partial class RepairMissingChatSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF OBJECT_ID(N'[ChatSessions]', N'U') IS NULL
            BEGIN
                CREATE TABLE [ChatSessions] (
                    [Id] nvarchar(64) NOT NULL,
                    [UserId] int NOT NULL,
                    [StartedAt] datetime2 NOT NULL,
                    [EndedAt] datetime2 NULL,
                    CONSTRAINT [PK_ChatSessions] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_ChatSessions_Users_UserId]
                        FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
                );

                CREATE INDEX [IX_ChatSessions_UserId_EndedAt_StartedAt]
                    ON [ChatSessions] ([UserId], [EndedAt], [StartedAt]);
            END;
            """);

        migrationBuilder.Sql("""
            IF OBJECT_ID(N'[ChatMessages]', N'U') IS NULL
            BEGIN
                CREATE TABLE [ChatMessages] (
                    [Id] int NOT NULL IDENTITY,
                    [UserId] int NOT NULL,
                    [Message] nvarchar(4000) NOT NULL,
                    [Sender] nvarchar(10) NOT NULL,
                    [Timestamp] datetime2 NOT NULL,
                    [ChatSessionId] nvarchar(64) NOT NULL,
                    CONSTRAINT [PK_ChatMessages] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_ChatMessages_ChatSessions_ChatSessionId]
                        FOREIGN KEY ([ChatSessionId]) REFERENCES [ChatSessions] ([Id]) ON DELETE CASCADE,
                    CONSTRAINT [FK_ChatMessages_Users_UserId]
                        FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id])
                );

                CREATE INDEX [IX_ChatMessages_ChatSessionId_Timestamp]
                    ON [ChatMessages] ([ChatSessionId], [Timestamp]);
                CREATE INDEX [IX_ChatMessages_UserId_Timestamp]
                    ON [ChatMessages] ([UserId], [Timestamp]);
            END;
            """);

        migrationBuilder.Sql("""
            IF OBJECT_ID(N'[ShoppingListItems]', N'U') IS NULL
            BEGIN
                CREATE TABLE [ShoppingListItems] (
                    [Id] int NOT NULL IDENTITY,
                    [UserId] int NOT NULL,
                    [ProductId] int NOT NULL,
                    [ProductName] nvarchar(150) NOT NULL,
                    [Price] decimal(18,2) NOT NULL,
                    [SelectedDate] datetime2 NOT NULL,
                    CONSTRAINT [PK_ShoppingListItems] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_ShoppingListItems_Users_UserId]
                        FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
                );

                CREATE UNIQUE INDEX [IX_ShoppingListItems_UserId_ProductId]
                    ON [ShoppingListItems] ([UserId], [ProductId]);
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // This is a repair migration. Its conditional Up method may not have
        // created every table, so rollback must not remove existing data.
    }
}
