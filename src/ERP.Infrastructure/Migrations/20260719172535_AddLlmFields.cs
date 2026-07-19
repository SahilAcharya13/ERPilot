using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLlmFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CompletionTokens",
                table: "AiActionLogs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ForcedToPendingBySafety",
                table: "AiActionLogs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PromptTokens",
                table: "AiActionLogs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TotalTokens",
                table: "AiActionLogs",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompletionTokens",
                table: "AiActionLogs");

            migrationBuilder.DropColumn(
                name: "ForcedToPendingBySafety",
                table: "AiActionLogs");

            migrationBuilder.DropColumn(
                name: "PromptTokens",
                table: "AiActionLogs");

            migrationBuilder.DropColumn(
                name: "TotalTokens",
                table: "AiActionLogs");
        }
    }
}
