using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Downora.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class EmbedTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EmbedCoverArt",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "EmbedMetadata",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmbedCoverArt",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "EmbedMetadata",
                table: "AppSettings");
        }
    }
}
