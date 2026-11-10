using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Downora.Core.Enums;
using Downora.Core.Interfaces;
using Downora.Core.Models;
using Downora.Core.Services;
using Downora.Infrastructure.Downloads;
using Downora.Infrastructure.Extraction;
using Downora.Infrastructure.Processes;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Downora.Infrastructure.Tests;

/// <summary>Everything that reads what a website or a tool printed must survive any input without crashing.</summary>
public class MalformedInputTests
{
    [Theory]
    [InlineData("yt_video.json")]
    [InlineData("yt_playlist.json")]
    [InlineData("sc_track.json")]
    public void Truncated_json_only_ever_fails_as_a_json_error(string fixture)
    {
        var json = ExtractionTests.Fixture(fixture);

        // Cut the document at many points: yt-dlp killed mid-output, a full pipe, a timeout.
        for (var length = 0; length < json.Length; length += Math.Max(1, json.Length / 400))
        {
            AssertParsesOrFailsAsJson(json[..length]);
        }
    }

    [Theory]
    [InlineData("yt_video.json")]
    [InlineData("yt_playlist.json")]
    public void Json_with_random_characters_changed_never_crashes_the_parser(string fixture)
    {
        var original = ExtractionTests.Fixture(fixture);
        var random = new Random(99);
        const string noise = "{}[]\",:0123456789nulltruefalse-.eE\\ \n";

        for (var i = 0; i < 400; i++)
        {
            var chars = original.ToCharArray();
            for (var edit = 0; edit < 1 + random.Next(6); edit++)
            {
                chars[random.Next(chars.Length)] = noise[random.Next(noise.Length)];
            }

            AssertParsesOrFailsAsJson(new string(chars));
        }
    }

    [Theory]
    [InlineData("""{"entries": 5}""")]
    [InlineData("""{"entries": "x", "_type": "playlist"}""")]
    [InlineData("""{"_type": "playlist", "entries": [1, "a", null, [], {"id": 5, "title": [1], "url": {}, "duration": "abc"}]}""")]
    [InlineData("""{"id": 1, "title": 2, "formats": "x", "duration": {}}""")]
    [InlineData("""{"id": "a", "title": "b", "formats": [null, 1, "x", {"format_id": 7, "width": "wide", "filesize": "big"}]}""")]
    [InlineData("""{"title": "t", "duration": 1e999}""")]
    [InlineData("""{"title": "t", "duration": -5}""")]
    [InlineData("""{"title": "t", "duration": 99999999999999999999}""")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("42")]
    [InlineData("\"text\"")]
    [InlineData("{}")]
    public void Json_of_the_wrong_shape_never_crashes_the_parser(string json) => AssertParsesOrFailsAsJson(json);

    [Fact]
    public void Absurdly_nested_json_is_a_json_error_not_a_stack_overflow() =>
        AssertParsesOrFailsAsJson(new string('[', 100_000) + new string(']', 100_000));

    [Fact]
    public void Progress_lines_that_are_garbage_never_crash_the_parser()
    {
        var random = new Random(5);
        string[] heads = ["DWN|", "PP|", "FILE|", "DWN|downloading|", "DWN|finished|1|2|3|4|5|6|7|", "[download] 10%", "ERROR: x", "", "|||||||||"];
        const string noise = "0123456789.-NA|abc ";

        for (var i = 0; i < 5000; i++)
        {
            var builder = new StringBuilder(heads[random.Next(heads.Length)]);
            for (var c = 0; c < random.Next(0, 60); c++)
            {
                builder.Append(noise[random.Next(noise.Length)]);
            }

            _ = YtDlpProgressParser.Parse(builder.ToString());
        }

        Assert.Null(YtDlpProgressParser.Parse(null));
        Assert.Null(YtDlpProgressParser.Parse(string.Empty));
    }

    [Fact]
    public void The_error_classifier_copes_with_any_text()
    {
        var random = new Random(11);
        const string noise = "ERROR: Sign in to confirm DRM protected Private video cookies 403 HTTP Error  \n\r\t\0é";
        for (var i = 0; i < 3000; i++)
        {
            var text = string.Concat(Enumerable.Range(0, random.Next(0, 80)).Select(_ => noise[random.Next(noise.Length)]));

            var (kind, _) = ErrorClassifier.Classify(text);

            Assert.True(Enum.IsDefined(kind));
        }

        Assert.Equal(DownloadErrorKind.Unknown, ErrorClassifier.Classify(null).Kind);
    }

    private static void AssertParsesOrFailsAsJson(string json)
    {
        try
        {
            var result = YtDlpJsonParser.Parse(json, SourcePlatform.YouTube);
            Assert.True(Enum.IsDefined(result.Kind));
        }
        catch (JsonException)
        {
            // The only failure the caller handles, and it reports it as "couldn't read".
        }
    }
}

/// <summary>Playlists and histories can be big. These checks use generous limits, so they only fail on a real slowdown.</summary>
public sealed class ScaleTests : IAsyncDisposable
{
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(15);
    private TestEnvironment? _env;

    public async ValueTask DisposeAsync()
    {
        if (_env is not null)
        {
            await _env.DisposeAsync();
        }
    }

    [Fact]
    public void A_playlist_of_five_thousand_items_is_parsed_quickly()
    {
        var json = new StringBuilder("""{"_type":"playlist","id":"PL","title":"Huge","entries":[""");
        for (var i = 0; i < 5000; i++)
        {
            json.Append(CultureInfo.InvariantCulture, $$"""{"id":"v{{i}}","url":"https://www.youtube.com/watch?v=v{{i}}","title":"Video {{i}}","duration":{{60 + i}},"uploader":"u"}""");
            json.Append(i < 4999 ? ',' : ' ');
        }

        json.Append("]}");

        var clock = Stopwatch.StartNew();
        var result = YtDlpJsonParser.Parse(json.ToString(), SourcePlatform.YouTube);
        clock.Stop();

        Assert.Equal(5000, result.Playlist!.Entries.Count);
        Assert.True(clock.Elapsed < Budget, $"Parsing took {clock.Elapsed}");
    }

    [Fact]
    public async Task Twenty_thousand_jobs_in_the_database_do_not_slow_the_queue_or_history()
    {
        _env = await TestEnvironment.StartAsync();
        var now = DateTime.UtcNow;
        await using (var db = await _env.CreateDbAsync())
        {
            for (var chunk = 0; chunk < 20; chunk++)
            {
                for (var i = 0; i < 1000; i++)
                {
                    var n = (chunk * 1000) + i;
                    db.DownloadJobs.Add(new DownloadJob
                    {
                        SourceUrl = $"https://e.com/{n}",
                        NormalizedUrl = $"https://e.com/{n}",
                        OutputDirectory = "x",
                        Title = $"Title {n}",
                        Status = n % 50 == 0 ? DownloadStatus.Waiting : DownloadStatus.Completed,
                        QueueOrder = n,
                        CreatedAtUtc = now.AddMinutes(-n),
                        CompletedAtUtc = now.AddMinutes(-n),
                    });
                }

                await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            }
        }

        var queries = _env.Get<IDownloadJobQueries>();
        var clock = Stopwatch.StartNew();

        var active = await queries.GetActiveAsync(TestContext.Current.CancellationToken);
        var history = await queries.GetHistoryAsync(new HistoryQuery("Title 123"), TestContext.Current.CancellationToken);
        var counts = await queries.CountByStatusAsync(TestContext.Current.CancellationToken);
        var stats = await queries.GetStatisticsAsync(TestContext.Current.CancellationToken);
        var pruned = await queries.PruneHistoryAsync(now.AddDays(-5), TestContext.Current.CancellationToken);

        clock.Stop();
        Assert.Equal(400, active.Count);
        Assert.NotEmpty(history);
        Assert.Equal(19_600, counts[DownloadStatus.Completed]);
        Assert.True(stats.TotalCompleted > 0);
        Assert.True(pruned > 0);
        Assert.True(clock.Elapsed < Budget, $"Queries took {clock.Elapsed}");
    }

    [Fact]
    public async Task A_library_of_ten_thousand_files_searches_and_sorts_quickly()
    {
        _env = await TestEnvironment.StartAsync();
        await using (var db = await _env.CreateDbAsync())
        {
            for (var chunk = 0; chunk < 10; chunk++)
            {
                for (var i = 0; i < 1000; i++)
                {
                    var n = (chunk * 1000) + i;
                    db.MediaItems.Add(new MediaItem
                    {
                        FilePath = $@"D:\Media\file{n}.mp4",
                        Title = $"Song number {n}",
                        Artist = n % 2 == 0 ? "Even" : "Odd",
                        MediaKind = n % 3 == 0 ? MediaKind.Audio : MediaKind.Video,
                        FileSizeBytes = n,
                        Duration = TimeSpan.FromSeconds(n),
                        AddedAtUtc = DateTime.UtcNow.AddSeconds(-n),
                    });
                }

                await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            }
        }

        var library = _env.Get<IMediaLibraryService>();
        var clock = Stopwatch.StartNew();

        foreach (var sort in Enum.GetValues<LibrarySort>())
        {
            var page = await library.QueryAsync(new LibraryQuery(null, null, sort, true, 120), TestContext.Current.CancellationToken);
            Assert.Equal(120, page.Count);
        }

        var found = await library.QueryAsync(new LibraryQuery("number 9999"), TestContext.Current.CancellationToken);
        var audio = await library.QueryAsync(new LibraryQuery("Even", MediaKind.Audio, Take: 5000), TestContext.Current.CancellationToken);

        clock.Stop();
        Assert.Single(found);
        Assert.All(audio, a => Assert.Equal(MediaKind.Audio, a.MediaKind));
        Assert.True(clock.Elapsed < Budget, $"Library queries took {clock.Elapsed}");
    }

    [Fact]
    public async Task Searching_with_wildcard_characters_matches_them_literally()
    {
        _env = await TestEnvironment.StartAsync();
        await using (var db = await _env.CreateDbAsync())
        {
            db.MediaItems.Add(new MediaItem { FilePath = @"D:\a.mp4", Title = "100% real" });
            db.MediaItems.Add(new MediaItem { FilePath = @"D:\b.mp4", Title = "snake_case" });
            db.MediaItems.Add(new MediaItem { FilePath = @"D:\c.mp4", Title = "plain" });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var library = _env.Get<IMediaLibraryService>();

        Assert.Equal("100% real", Assert.Single(await library.QueryAsync(new LibraryQuery("%"), TestContext.Current.CancellationToken)).Title);
        Assert.Equal("snake_case", Assert.Single(await library.QueryAsync(new LibraryQuery("_"), TestContext.Current.CancellationToken)).Title);
        Assert.Empty(await library.QueryAsync(new LibraryQuery("'; DROP TABLE MediaItems; --"), TestContext.Current.CancellationToken));
        Assert.Equal(3, (await library.QueryAsync(new LibraryQuery(), TestContext.Current.CancellationToken)).Count);
    }
}

public class HardenedToolCallTests
{
    private static readonly DownloadLayout Layout = new(@"C:\tools\ffmpeg.exe", null, @"D:\Downloads", @"D:\Downloads\.downora-partial\abc", "Song");

    [Fact]
    public void Downloads_never_load_yt_dlp_plugins_or_config()
    {
        var request = new DownloadRequest { JobId = Guid.NewGuid(), Url = "https://example.com/a", OutputDirectory = @"D:\Downloads" };

        var args = YtDlpDownloadArguments.Build(request, Layout);

        Assert.Contains("--no-plugin-dirs", args);
        Assert.Contains("--ignore-config", args);
        Assert.DoesNotContain(args, a => a.StartsWith("--exec", StringComparison.Ordinal) || a == "--plugin-dirs");
    }

    [Fact]
    public void Analysis_never_loads_yt_dlp_plugins_or_config()
    {
        var tools = Substitute.For<IToolManager>();
        tools.GetPath(ToolKind.YtDlp).Returns("C:/tools/yt-dlp.exe");
        var settings = Substitute.For<ISettingsService>();
        settings.Current.Returns(_ => AppSettings.CreateDefault(@"C:\Downloads"));
        var extractor = new YtDlpMediaExtractor(tools, Substitute.For<IProcessRunner>(), settings, NullLogger<YtDlpMediaExtractor>.Instance);
        var url = new UrlValidator(new SourceDetector()).Validate("https://example.com/a");

        var args = extractor.BuildArguments(url);

        Assert.Contains("--no-plugin-dirs", args);
        Assert.Contains("--ignore-config", args);
        Assert.Equal(["--", "https://example.com/a"], args.TakeLast(2));
    }

    [Fact]
    public async Task A_caller_that_follows_the_output_does_not_have_it_buffered_as_well()
    {
        var runner = new ProcessRunner(NullLogger<ProcessRunner>.Instance);
        var dotnet = Environment.ProcessPath is { } p && Path.GetFileNameWithoutExtension(p) == "dotnet" ? p : "dotnet";
        var lines = new List<string>();

        var kept = await runner.RunAsync(new ProcessSpec(dotnet, ["--version"], CaptureStandardOutput: true), lines.Add, cancellationToken: TestContext.Current.CancellationToken);
        var followed = await runner.RunAsync(new ProcessSpec(dotnet, ["--version"], CaptureStandardOutput: false), cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotEmpty(kept.StandardOutput);
        Assert.Empty(followed.StandardOutput);
        Assert.Equal(0, followed.ExitCode);
        Assert.NotEmpty(lines);
    }
}
