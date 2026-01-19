using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Purrnet.Migrations
{
    /// <inheritdoc />
    public partial class SeedDefaultCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Table and relationship already created in a previous migration (AddCategories).
            // Only insert seed data here.
            migrationBuilder.InsertData(
                table: "Categories",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Utility" },
                    { 2, "Development" },
                    { 3, "CLI" },
                    { 4, "Tools" },
                    { 5, "UI" },
                    { 6, "GeneralLibrary" },
                    { 7, "MasmLibrary" },
                    { 8, "ObjectIRLibrary" },
                    { 9, "Game" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove seeded categories (do not drop tables created by other migrations)
            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValues: new object[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 });
        }
    }
}
