using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeTamer.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAchievementTaskLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserAchievements_TaskItems_TaskItemId",
                table: "UserAchievements");

            migrationBuilder.DropIndex(
                name: "IX_UserAchievements_TaskItemId",
                table: "UserAchievements");

            migrationBuilder.DropColumn(
                name: "TaskItemId",
                table: "UserAchievements");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TaskItemId",
                table: "UserAchievements",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserAchievements_TaskItemId",
                table: "UserAchievements",
                column: "TaskItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_UserAchievements_TaskItems_TaskItemId",
                table: "UserAchievements",
                column: "TaskItemId",
                principalTable: "TaskItems",
                principalColumn: "Id");
        }
    }
}
