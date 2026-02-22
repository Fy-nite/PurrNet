using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Purrnet.Migrations
{
    /// <inheritdoc />
    public partial class AddPackageReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PackageReviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PackageId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: true),
                    Rating = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    ReviewerName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ReviewerAvatarUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackageReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PackageReviews_Packages_PackageId",
                        column: x => x.PackageId,
                        principalTable: "Packages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PackageReviews_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PackageReviews_CreatedAt",
                table: "PackageReviews",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PackageReviews_PackageId",
                table: "PackageReviews",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_PackageReviews_PackageId_UserId",
                table: "PackageReviews",
                columns: new[] { "PackageId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PackageReviews_UserId",
                table: "PackageReviews",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PackageReviews");
        }
    }
}
