using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Downora.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class LibraryBackfill : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "LibraryBackfilled",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LibraryBackfilled",
                table: "AppSettings");
        }
    }
}
