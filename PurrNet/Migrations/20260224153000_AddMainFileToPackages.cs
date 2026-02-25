using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Purrnet.Migrations
{
    /// <inheritdoc />
    public partial class AddMainFileToPackages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MainFile",
                table: "Packages",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MainFile",
                table: "Packages");
        }
    }
}
