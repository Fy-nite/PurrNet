using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace purrnet.Migrations
{
    /// <inheritdoc />
    public partial class AddIsBannedAndIsOutdated : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsBanned",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsOutdated",
                table: "Packages",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Users_IsBanned",
                table: "Users",
                column: "IsBanned");

            migrationBuilder.CreateIndex(
                name: "IX_Packages_IsOutdated",
                table: "Packages",
                column: "IsOutdated");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_IsBanned",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Packages_IsOutdated",
                table: "Packages");

            migrationBuilder.DropColumn(
                name: "IsBanned",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsOutdated",
                table: "Packages");
        }
    }
}
