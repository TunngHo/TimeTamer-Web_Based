using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeTamer.Migrations
{
    /// <inheritdoc />
    public partial class TaskArchiveTrash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "TaskItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "TaskItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "TaskItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "TaskItems",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "TaskItems");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "TaskItems");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "TaskItems");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "TaskItems");
        }
    }
}
