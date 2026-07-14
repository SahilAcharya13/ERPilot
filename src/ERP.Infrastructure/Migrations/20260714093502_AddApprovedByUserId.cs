using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddApprovedByUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ApprovedByUserId",
                table: "AiActionLogs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: 1,
                column: "PasswordHash",
                value: "$2b$12$Hs4kRUkTrScs6j1Jfe3d.OP56.pLuzi1y43urhLqi74DVy40otKQ2");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: 2,
                column: "PasswordHash",
                value: "$2b$12$98PCQ1/QtR4RtogsOXooqO4q96jcauGMIda7K8R5fZ/Oh7FgZSQRm");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: 3,
                column: "PasswordHash",
                value: "$2b$12$xF03tsGBpgBEXVd9SKFigeOfHBcoSuxJHINWZ2Opj8IJvql4SXSrG");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: 4,
                column: "PasswordHash",
                value: "$2b$12$Wq6necim2kgRnkB1pPj5L.IJv86YxOOjIWU3Rgi1t8W7xaGLlTiNu");

            migrationBuilder.CreateIndex(
                name: "IX_AiActionLogs_ApprovedByUserId",
                table: "AiActionLogs",
                column: "ApprovedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_AiActionLogs_Users_ApprovedByUserId",
                table: "AiActionLogs",
                column: "ApprovedByUserId",
                principalTable: "Users",
                principalColumn: "UserID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AiActionLogs_Users_ApprovedByUserId",
                table: "AiActionLogs");

            migrationBuilder.DropIndex(
                name: "IX_AiActionLogs_ApprovedByUserId",
                table: "AiActionLogs");

            migrationBuilder.DropColumn(
                name: "ApprovedByUserId",
                table: "AiActionLogs");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: 1,
                column: "PasswordHash",
                value: "AQAAAAEAACcQAAAAEP1R3S8y... (hashed)");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: 2,
                column: "PasswordHash",
                value: "AQAAAAEAACcQAAAAEP1R3S8y... (hashed)");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: 3,
                column: "PasswordHash",
                value: "AQAAAAEAACcQAAAAEP1R3S8y... (hashed)");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: 4,
                column: "PasswordHash",
                value: "AQAAAAEAACcQAAAAEP1R3S8y... (hashed)");
        }
    }
}
