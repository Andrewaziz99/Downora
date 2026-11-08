using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Downora.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    DownloadDirectory = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    PreferredVideoContainer = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    PreferredAudioFormat = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    DefaultVideoQuality = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    DefaultAudioBitrateKbps = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxConcurrentDownloads = table.Column<int>(type: "INTEGER", nullable: false),
                    AutoAddToQueue = table.Column<bool>(type: "INTEGER", nullable: false),
                    AutoStartDownloads = table.Column<bool>(type: "INTEGER", nullable: false),
                    HistoryRetentionDays = table.Column<int>(type: "INTEGER", nullable: true),
                    FfmpegPathOverride = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    YtDlpAutoUpdate = table.Column<bool>(type: "INTEGER", nullable: false),
                    UseBrowserSession = table.Column<bool>(type: "INTEGER", nullable: false),
                    BrowserForCookies = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    LaunchAtStartup = table.Column<bool>(type: "INTEGER", nullable: false),
                    StartMinimized = table.Column<bool>(type: "INTEGER", nullable: false),
                    Theme = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Language = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Id);
                    table.CheckConstraint("CK_AppSettings_Singleton", "\"Id\" = 1");
                });

            migrationBuilder.CreateTable(
                name: "PlaylistBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceUrl = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    Uploader = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    ItemCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaylistBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DownloadJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceUrl = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: false),
                    NormalizedUrl = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: false),
                    Platform = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    Uploader = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    ThumbnailUrl = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: true),
                    Duration = table.Column<long>(type: "INTEGER", nullable: true),
                    MediaKind = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    QualityPreset = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    FormatSelector = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    Container = table.Column<string>(type: "TEXT", maxLength: 8, nullable: true),
                    AudioFormat = table.Column<string>(type: "TEXT", maxLength: 8, nullable: true),
                    AudioBitrateKbps = table.Column<int>(type: "INTEGER", nullable: true),
                    OutputDirectory = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    OutputPath = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ProgressPercent = table.Column<double>(type: "REAL", nullable: false),
                    DownloadedBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    TotalBytes = table.Column<long>(type: "INTEGER", nullable: true),
                    ErrorKind = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    RetryCount = table.Column<int>(type: "INTEGER", nullable: false),
                    QueueOrder = table.Column<long>(type: "INTEGER", nullable: false),
                    PlaylistBatchId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DownloadJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DownloadJobs_PlaylistBatches_PlaylistBatchId",
                        column: x => x.PlaylistBatchId,
                        principalTable: "PlaylistBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "MediaItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DownloadJobId = table.Column<Guid>(type: "TEXT", nullable: true),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    MediaKind = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    Artist = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Album = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Duration = table.Column<long>(type: "INTEGER", nullable: true),
                    FileSizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    Width = table.Column<int>(type: "INTEGER", nullable: true),
                    Height = table.Column<int>(type: "INTEGER", nullable: true),
                    VideoCodec = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    AudioCodec = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    BitrateKbps = table.Column<int>(type: "INTEGER", nullable: true),
                    ThumbnailPath = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    SourceUrl = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: true),
                    Platform = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    AddedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsMissing = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaItems_DownloadJobs_DownloadJobId",
                        column: x => x.DownloadJobId,
                        principalTable: "DownloadJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DownloadJobs_CreatedAtUtc",
                table: "DownloadJobs",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadJobs_NormalizedUrl_QualityPreset_Status",
                table: "DownloadJobs",
                columns: new[] { "NormalizedUrl", "QualityPreset", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_DownloadJobs_PlaylistBatchId",
                table: "DownloadJobs",
                column: "PlaylistBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadJobs_Status_QueueOrder",
                table: "DownloadJobs",
                columns: new[] { "Status", "QueueOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaItems_AddedAtUtc",
                table: "MediaItems",
                column: "AddedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_MediaItems_DownloadJobId",
                table: "MediaItems",
                column: "DownloadJobId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaItems_FilePath",
                table: "MediaItems",
                column: "FilePath",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaItems_Title",
                table: "MediaItems",
                column: "Title");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppSettings");

            migrationBuilder.DropTable(
                name: "MediaItems");

            migrationBuilder.DropTable(
                name: "DownloadJobs");

            migrationBuilder.DropTable(
                name: "PlaylistBatches");
        }
    }
}
