using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeTamer.Migrations
{
    /// <inheritdoc />
    public partial class AchievementTaskAndSharedSubTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TaskItemId",
                table: "UserAchievements",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedByName",
                table: "SubTasks",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreatedByUserId",
                table: "SubTasks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SharedSubTaskGroupId",
                table: "SubTasks",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserAchievements_TaskItemId",
                table: "UserAchievements",
                column: "TaskItemId");

            migrationBuilder.CreateIndex(
                name: "IX_SubTasks_CreatedByUserId",
                table: "SubTasks",
                column: "CreatedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_SubTasks_Users_CreatedByUserId",
                table: "SubTasks",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_UserAchievements_TaskItems_TaskItemId",
                table: "UserAchievements",
                column: "TaskItemId",
                principalTable: "TaskItems",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SubTasks_Users_CreatedByUserId",
                table: "SubTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_UserAchievements_TaskItems_TaskItemId",
                table: "UserAchievements");

            migrationBuilder.DropIndex(
                name: "IX_UserAchievements_TaskItemId",
                table: "UserAchievements");

            migrationBuilder.DropIndex(
                name: "IX_SubTasks_CreatedByUserId",
                table: "SubTasks");

            migrationBuilder.DropColumn(
                name: "TaskItemId",
                table: "UserAchievements");

            migrationBuilder.DropColumn(
                name: "CreatedByName",
                table: "SubTasks");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "SubTasks");

            migrationBuilder.DropColumn(
                name: "SharedSubTaskGroupId",
                table: "SubTasks");
        }
    }
}
