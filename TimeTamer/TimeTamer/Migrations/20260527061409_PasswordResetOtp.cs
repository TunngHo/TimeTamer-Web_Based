using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeTamer.Migrations
{
    /// <inheritdoc />
    public partial class PasswordResetOtp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PasswordResetOtpAttempts",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PasswordResetOtpAttempts",
                table: "Users");
        }
    }
}
