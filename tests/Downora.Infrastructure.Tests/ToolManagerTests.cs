using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Downora.Core.Enums;
using Downora.Core.Interfaces;
using Downora.Core.Models;
using Downora.Infrastructure.Services;
using Downora.Infrastructure.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Downora.Infrastructure.Tests;

/// <summary>Serves fixed bytes per URL, like a tiny GitHub releases mirror.</summary>
internal sealed class FakeHttp : HttpMessageHandler
{
    public Dictionary<string, byte[]> Files { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(Files.TryGetValue(request.RequestUri!.AbsoluteUri, out var bytes)
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) }
            : new HttpResponseMessage(HttpStatusCode.NotFound));
}

public sealed class ToolManagerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "downora-tools-" + Guid.NewGuid().ToString("N"));
    private readonly AppPaths _paths;
    private readonly FakeHttp _http = new();
    private readonly HttpClient _client;
    private readonly ISettingsService _settings = Substitute.For<ISettingsService>();

    public ToolManagerTests()
    {
        _paths = new AppPaths(Path.Combine(_root, "data"), Path.Combine(_root, "downloads"));
        _paths.EnsureCreated();
        _client = new HttpClient(_http);
        _settings.Current.Returns(AppSettings.CreateDefault(_paths.DefaultDownloadDirectory));
    }

    public void Dispose()
    {
        _client.Dispose();
        _http.Dispose();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static readonly Uri ExeUrl = new("https://example.test/yt-dlp.exe");
    private static readonly Uri SumsUrl = new("https://example.test/SHA2-256SUMS");
    private static readonly Uri ZipUrl = new("https://example.test/ffmpeg.zip");
    private static readonly Uri ZipSumUrl = new("https://example.test/checksums.sha256");

    private static ToolSource? Sources(ToolKind tool) => tool switch
    {
        ToolKind.YtDlp => new ToolSource(ExeUrl, SumsUrl, "yt-dlp.exe", null),
        ToolKind.Ffmpeg or ToolKind.Ffprobe => new ToolSource(ZipUrl, ZipSumUrl, "ffmpeg.zip", [new(ToolKind.Ffmpeg, "ffmpeg.exe"), new(ToolKind.Ffprobe, "ffprobe.exe")]),
        _ => null,
    };

    private ToolManager Manager(IProcessRunner? runner = null, bool canInstall = true, string? bundled = null) =>
        new(_paths, _settings, runner ?? new FakeProcessRunner(_ => FakeProcessRunner.Ok("")), _client,
            NullLogger<ToolManager>.Instance, bundled ?? Path.Combine(_root, "bundled"), Sources, canInstall);

    private static string Sha256(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private string Installed(ToolKind tool) => Path.Combine(_paths.ToolsDirectory, ToolCatalog.ExecutableName(tool));

    private static byte[] Zip(params (string Path, string Content)[] entries)
    {
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, content) in entries)
            {
                using var writer = new StreamWriter(archive.CreateEntry(path).Open());
                writer.Write(content);
            }
        }

        return memory.ToArray();
    }

    [Fact]
    public async Task Exe_download_is_verified_and_installed_with_progress()
    {
        var exe = Encoding.UTF8.GetBytes("pretend yt-dlp binary");
        _http.Files[ExeUrl.AbsoluteUri] = exe;
        _http.Files[SumsUrl.AbsoluteUri] = Encoding.UTF8.GetBytes($"{new string('0', 64)}  yt-dlp\n{Sha256(exe)}  yt-dlp.exe\n");
        var reports = new List<ToolInstallProgress>();

        await Manager().InstallAsync(ToolKind.YtDlp, new SynchronousProgress<ToolInstallProgress>(reports.Add), TestContext.Current.CancellationToken);

        Assert.Equal(exe, await File.ReadAllBytesAsync(Installed(ToolKind.YtDlp), TestContext.Current.CancellationToken));
        Assert.Equal(exe.Length, reports[^1].BytesReceived);
        Assert.Empty(Directory.GetFiles(_paths.ToolsDirectory, "*.partial"));
    }

    [Fact]
    public async Task Tampered_download_is_rejected_and_nothing_is_installed()
    {
        _http.Files[ExeUrl.AbsoluteUri] = Encoding.UTF8.GetBytes("tampered");
        _http.Files[SumsUrl.AbsoluteUri] = Encoding.UTF8.GetBytes($"{Sha256(Encoding.UTF8.GetBytes("original"))}  yt-dlp.exe\n");

        var error = await Assert.ThrowsAsync<ToolInstallException>(() => Manager().InstallAsync(ToolKind.YtDlp, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("checksum", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Installed(ToolKind.YtDlp)));
        Assert.Empty(Directory.GetFiles(_paths.ToolsDirectory));
    }

    [Fact]
    public async Task Ffmpeg_zip_installs_both_ffmpeg_and_ffprobe()
    {
        var zip = Zip(("ffmpeg-master-latest-win64-lgpl/bin/ffmpeg.exe", "ffmpeg!"), ("ffmpeg-master-latest-win64-lgpl/bin/ffprobe.exe", "ffprobe!"), ("ffmpeg-master-latest-win64-lgpl/LICENSE.txt", "LGPL"));
        _http.Files[ZipUrl.AbsoluteUri] = zip;
        _http.Files[ZipSumUrl.AbsoluteUri] = Encoding.UTF8.GetBytes($"{Sha256(zip)}  ffmpeg.zip\n");

        await Manager().InstallAsync(ToolKind.Ffprobe, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("ffmpeg!", await File.ReadAllTextAsync(Installed(ToolKind.Ffmpeg), TestContext.Current.CancellationToken));
        Assert.Equal("ffprobe!", await File.ReadAllTextAsync(Installed(ToolKind.Ffprobe), TestContext.Current.CancellationToken));
        Assert.Equal(2, Directory.GetFiles(_paths.ToolsDirectory).Length);
    }

    [Fact]
    public async Task Checksum_list_without_our_file_is_refused()
    {
        _http.Files[ExeUrl.AbsoluteUri] = [1, 2, 3];
        _http.Files[SumsUrl.AbsoluteUri] = Encoding.UTF8.GetBytes($"{new string('a', 64)}  yt-dlp_linux\n");

        await Assert.ThrowsAsync<ToolInstallException>(() => Manager().InstallAsync(ToolKind.YtDlp, cancellationToken: TestContext.Current.CancellationToken));
        Assert.False(File.Exists(Installed(ToolKind.YtDlp)));
    }

    [Fact]
    public async Task Download_errors_become_install_errors()
    {
        // Nothing served: 404.
        await Assert.ThrowsAsync<ToolInstallException>(() => Manager().InstallAsync(ToolKind.YtDlp, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Install_is_refused_where_it_is_not_supported() =>
        await Assert.ThrowsAsync<ToolInstallException>(() => Manager(canInstall: false).InstallAsync(ToolKind.YtDlp, cancellationToken: TestContext.Current.CancellationToken));

    [Fact]
    public async Task Status_reports_installed_tools_with_versions()
    {
        await File.WriteAllTextAsync(Installed(ToolKind.YtDlp), "x", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Installed(ToolKind.Deno), "x", TestContext.Current.CancellationToken);
        var runner = new FakeProcessRunner(spec => FakeProcessRunner.Ok(Path.GetFileName(spec.FileName).StartsWith("yt-dlp", StringComparison.Ordinal)
            ? "2026.10.26\n"
            : "deno 2.9.8 (stable, release, x86_64-pc-windows-msvc)\nv8 14.0\n"));

        var status = await Manager(runner).GetStatusAsync(TestContext.Current.CancellationToken);

        Assert.Equal("2026.10.26", status.Single(s => s.Tool == ToolKind.YtDlp).Version);
        Assert.Equal("2.9.8", status.Single(s => s.Tool == ToolKind.Deno).Version);
        Assert.False(status.Single(s => s.Tool == ToolKind.Ffmpeg).IsInstalled);
        Assert.Null(Manager().GetPath(ToolKind.Ffprobe));
    }

    [Fact]
    public async Task Bundled_tools_are_copied_once_and_never_overwrite_updates()
    {
        var bundled = Path.Combine(_root, "bundled");
        Directory.CreateDirectory(bundled);
        await File.WriteAllTextAsync(Path.Combine(bundled, ToolCatalog.ExecutableName(ToolKind.YtDlp)), "bundled", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(bundled, ToolCatalog.ExecutableName(ToolKind.Deno)), "bundled deno", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Installed(ToolKind.YtDlp), "self-updated", TestContext.Current.CancellationToken);

        await Manager(bundled: bundled).PrepareAsync(TestContext.Current.CancellationToken);

        Assert.Equal("self-updated", await File.ReadAllTextAsync(Installed(ToolKind.YtDlp), TestContext.Current.CancellationToken));
        Assert.Equal("bundled deno", await File.ReadAllTextAsync(Installed(ToolKind.Deno), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Custom_ffmpeg_path_from_settings_wins()
    {
        var custom = Path.Combine(_root, "custom-ffmpeg.exe");
        await File.WriteAllTextAsync(custom, "x", TestContext.Current.CancellationToken);
        var settings = AppSettings.CreateDefault(_paths.DefaultDownloadDirectory);
        settings.FfmpegPathOverride = custom;
        _settings.Current.Returns(settings);

        Assert.Equal(custom, Manager().GetPath(ToolKind.Ffmpeg));
    }

    [Fact]
    public async Task Failed_self_update_explains_why()
    {
        await File.WriteAllTextAsync(Installed(ToolKind.YtDlp), "x", TestContext.Current.CancellationToken);
        var runner = new FakeProcessRunner(_ => FakeProcessRunner.Fail("ERROR: Unable to write to C:\\tools\\yt-dlp.exe; try running as administrator"));

        var error = await Assert.ThrowsAsync<ToolInstallException>(() => Manager(runner).UpdateYtDlpAsync(TestContext.Current.CancellationToken));

        Assert.Contains("Unable to write", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("f5389d3698ecde4ef06f4e6b1ce0294fe9ac1396228c8990b8d3416d6d177a7d  yt-dlp.exe\nabc", "yt-dlp.exe", "f5389d3698ecde4ef06f4e6b1ce0294fe9ac1396228c8990b8d3416d6d177a7d")]
    [InlineData("92E617D4D4291A7D808CB35F765AB32A11967DC759366F4ADAAEC1022CC3B152 *ffmpeg.zip", "ffmpeg.zip", "92e617d4d4291a7d808cb35f765ab32a11967dc759366f4adaaec1022cc3b152")]
    [InlineData("\nAlgorithm : SHA256\nHash      : CBA84B26E0DF5AE7F74FC66B86A95B866872A86292EBD84FA1E959927EDD93BF\nPath      : deno.zip\n", "deno.zip", "cba84b26e0df5ae7f74fc66b86a95b866872a86292ebd84fa1e959927edd93bf")]
    [InlineData("0000000000000000000000000000000000000000000000000000000000000000  other.exe", "yt-dlp.exe", null)]
    [InlineData("no hashes here", "yt-dlp.exe", null)]
    public void Checksum_files_in_every_published_format_are_understood(string text, string file, string? expected) =>
        Assert.Equal(expected, ToolCatalog.ParseChecksum(text, file));

    [Theory]
    [InlineData(ToolKind.YtDlp, "2026.10.26\n", "2026.10.26")]
    [InlineData(ToolKind.Ffmpeg, "ffmpeg version N-121583-g3e4a8b5 Copyright (c) 2000-2026\nbuilt with gcc", "N-121583-g3e4a8b5")]
    [InlineData(ToolKind.Ffprobe, "ffprobe version 7.1 Copyright", "7.1")]
    [InlineData(ToolKind.Deno, "deno 2.9.8 (stable, release, x86_64-pc-windows-msvc)", "2.9.8")]
    [InlineData(ToolKind.YtDlp, "", null)]
    public void Versions_are_read_from_each_tool(ToolKind tool, string output, string? expected) =>
        Assert.Equal(expected, ToolManager.ParseVersion(tool, output));

    [Fact]
    public void Real_catalog_points_at_official_releases()
    {
        foreach (var tool in Enum.GetValues<ToolKind>())
        {
            var source = ToolCatalog.SourceFor(tool);
            Assert.NotNull(source);
            Assert.Equal("https", source.DownloadUrl.Scheme);
            Assert.Equal("github.com", source.DownloadUrl.Host);
            Assert.Equal("github.com", source.ChecksumUrl.Host);
        }
    }

    /// <summary><see cref="Progress{T}"/> posts to the thread pool; tests need reports in order and immediately.</summary>
    private sealed class SynchronousProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
