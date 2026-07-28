using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIShoppingAssistant.Migrations
{
    /// <inheritdoc />
    public partial class RemoveBudgetAlertEnabled : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('UserPreferences', 'BudgetAlertEnabled') IS NOT NULL
                BEGIN
                    DECLARE @constraintName sysname;

                    SELECT @constraintName = [d].[name]
                    FROM [sys].[default_constraints] [d]
                    INNER JOIN [sys].[columns] [c]
                        ON [d].[parent_column_id] = [c].[column_id]
                        AND [d].[parent_object_id] = [c].[object_id]
                    WHERE [d].[parent_object_id] = OBJECT_ID(N'[UserPreferences]')
                        AND [c].[name] = N'BudgetAlertEnabled';

                    IF @constraintName IS NOT NULL
                    BEGIN
                        EXEC(N'ALTER TABLE [UserPreferences] DROP CONSTRAINT [' + @constraintName + '];');
                    END

                    ALTER TABLE [UserPreferences] DROP COLUMN [BudgetAlertEnabled];
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "BudgetAlertEnabled",
                table: "UserPreferences",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
