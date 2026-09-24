using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeTamer.Migrations
{
    /// <inheritdoc />
    public partial class ReminderWebEmailNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EmailSentAt",
                table: "Reminders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WebDismissedAt",
                table: "Reminders",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmailSentAt",
                table: "Reminders");

            migrationBuilder.DropColumn(
                name: "WebDismissedAt",
                table: "Reminders");
        }
    }
}
