using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeTamer.Migrations
{
    /// <inheritdoc />
    public partial class AdminUserCodesAndApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdminCode",
                table: "Users",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserCode",
                table: "Users",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "AdminUserLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AdminId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminUserLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdminUserLinks_Users_AdminId",
                        column: x => x.AdminId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AdminUserLinks_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
            migrationBuilder.Sql(@"
                UPDATE [Users]
                SET [UserCode] = CONCAT('USR-', RIGHT(CONCAT('00000000', [Id]), 8))
                WHERE [UserCode] IS NULL OR [UserCode] = '';

                UPDATE [Users]
                SET [AdminCode] = CONCAT('ADM-', RIGHT(CONCAT('00000000', [Id]), 8))
                WHERE [IsAdmin] = CAST(1 AS bit) AND ([AdminCode] IS NULL OR [AdminCode] = '');
            ");


            migrationBuilder.CreateIndex(
                name: "IX_Users_AdminCode",
                table: "Users",
                column: "AdminCode");

            migrationBuilder.CreateIndex(
                name: "IX_Users_UserCode",
                table: "Users",
                column: "UserCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdminUserLinks_AdminId_UserId",
                table: "AdminUserLinks",
                columns: new[] { "AdminId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdminUserLinks_UserId",
                table: "AdminUserLinks",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminUserLinks");

            migrationBuilder.DropIndex(
                name: "IX_Users_AdminCode",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_UserCode",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AdminCode",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "UserCode",
                table: "Users");
        }
    }
}

