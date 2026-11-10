using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Downora.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class TrayAndMaintenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CloseToTray",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastToolCheckUtc",
                table: "AppSettings",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CloseToTray",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "LastToolCheckUtc",
                table: "AppSettings");
        }
    }
}
