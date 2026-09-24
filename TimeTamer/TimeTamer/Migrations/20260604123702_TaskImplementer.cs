using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeTamer.Migrations
{
    /// <inheritdoc />
    public partial class TaskImplementer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Implementer",
                table: "TaskItems",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Implementer",
                table: "TaskItems");
        }
    }
}
