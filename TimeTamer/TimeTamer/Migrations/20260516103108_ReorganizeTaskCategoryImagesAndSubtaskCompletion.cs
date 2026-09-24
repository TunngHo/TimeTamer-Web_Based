using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeTamer.Migrations
{
    public partial class ReorganizeTaskCategoryImagesAndSubtaskCompletion : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CategoryId",
                table: "TaskItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "TaskItems",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "SubTasks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaskType",
                table: "Categories",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Work");

            migrationBuilder.Sql(@"
                UPDATE Categories
                SET TaskType = CASE
                    WHEN Name IN ('Work', 'Study', 'Hangout', 'Sport', 'Daily') THEN Name
                    ELSE 'Work'
                END
                WHERE TaskType IS NULL OR TaskType = '';

                INSERT INTO Categories (Name, ColorCode, UserId, TaskType)
                SELECT DISTINCT t.TaskType, '#0d6efd', t.UserId, t.TaskType
                FROM TaskItems t
                WHERE t.TaskType IS NOT NULL
                  AND t.TaskType <> ''
                  AND NOT EXISTS (
                      SELECT 1 FROM Categories c
                      WHERE c.UserId = t.UserId AND c.TaskType = t.TaskType
                  );

                UPDATE t
                SET CategoryId = c.Id
                FROM TaskItems t
                INNER JOIN Categories c ON c.UserId = t.UserId AND c.TaskType = t.TaskType
                WHERE t.CategoryId IS NULL;
            ");

            migrationBuilder.CreateIndex(
                name: "IX_TaskItems_CategoryId",
                table: "TaskItems",
                column: "CategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_TaskItems_Categories_CategoryId",
                table: "TaskItems",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TaskItems_Categories_CategoryId",
                table: "TaskItems");

            migrationBuilder.DropIndex(
                name: "IX_TaskItems_CategoryId",
                table: "TaskItems");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "TaskItems");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "TaskItems");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "SubTasks");

            migrationBuilder.DropColumn(
                name: "TaskType",
                table: "Categories");
        }
    }
}
