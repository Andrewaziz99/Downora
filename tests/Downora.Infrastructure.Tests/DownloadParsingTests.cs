using Downora.Core.Models;
using Downora.Infrastructure.Downloads;

namespace Downora.Infrastructure.Tests;

/// <summary>The progress parser and tracker are pure, so they are tested against output captured from yt-dlp.</summary>
public class DownloadParsingTests
{
    private static string[] Lines(string fixture) =>
        ExtractionTests.Fixture(fixture).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    // ---- Parser ----

    [Fact]
    public void A_progress_line_is_read_field_by_field()
    {
        var line = Assert.IsType<DownloadLine>(YtDlpProgressParser.Parse("DWN|downloading|15360|223779|NA|726433.8163859008|12|NA|NA|395"));

        Assert.Equal("downloading", line.Status);
        Assert.Equal(15360, line.DownloadedBytes);
        Assert.Equal(223779, line.TotalBytes);
        Assert.False(line.IsTotalEstimated);
        Assert.Equal(726433.8163859008, line.BytesPerSecond);
        Assert.Equal(12, line.EtaSeconds);
        Assert.Null(line.FragmentIndex);
        Assert.Null(line.FragmentCount);
        Assert.Equal("395", line.StreamId);
        Assert.False(line.IsFinished);
    }

    [Fact]
    public void Missing_values_are_null_and_an_estimated_total_is_flagged()
    {
        var line = Assert.IsType<DownloadLine>(YtDlpProgressParser.Parse("DWN|downloading|1024|NA|900000.5|NA|NA|3|20|301"));

        Assert.Equal(900000, line.TotalBytes);
        Assert.True(line.IsTotalEstimated);
        Assert.Null(line.BytesPerSecond);
        Assert.Null(line.EtaSeconds);
        Assert.Equal(3, line.FragmentIndex);
        Assert.Equal(20, line.FragmentCount);
    }

    [Fact]
    public void A_finished_stream_is_recognised()
    {
        var line = Assert.IsType<DownloadLine>(YtDlpProgressParser.Parse("DWN|finished|223779|223779|NA|301617.01|NA|NA|NA|395"));
        Assert.True(line.IsFinished);
    }

    [Fact]
    public void Post_processing_steps_are_read()
    {
        var started = Assert.IsType<PostProcessLine>(YtDlpProgressParser.Parse("PP|started|Merger"));
        var finished = Assert.IsType<PostProcessLine>(YtDlpProgressParser.Parse("PP|finished|MoveFiles"));

        Assert.True(started.IsStarted);
        Assert.Equal("Merger", started.Processor);
        Assert.False(finished.IsStarted);
        Assert.Equal("MoveFiles", finished.Processor);
    }

    [Fact]
    public void The_final_path_is_kept_whole_including_spaces_and_bars()
    {
        var file = Assert.IsType<FileLine>(YtDlpProgressParser.Parse(@"FILE|C:\Users\me\Downloads\Artist | Song - live.mp3"));
        Assert.Equal(@"C:\Users\me\Downloads\Artist | Song - live.mp3", file.Path);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("WARNING: [youtube] Falling back to a different client")]
    [InlineData("[download] Destination: x.mp4")]
    [InlineData("DWN|downloading")]
    [InlineData("DWN|downloading|abc|NA|NA|NA|NA|NA|NA|1")]
    [InlineData("DWN|downloading|-5|NA|NA|NA|NA|NA|NA|1")]
    [InlineData("DWN|downloading|Infinity|NA|NA|NA|NA|NA|NA|1")]
    [InlineData("PP|started")]
    [InlineData("FILE|")]
    public void Anything_else_is_ignored(string? text) => Assert.Null(YtDlpProgressParser.Parse(text));

    // ---- Tracker, replaying whole downloads ----

    private static List<DownloadProgress> Replay(string fixture, long? estimate)
    {
        var tracker = new ProgressTracker(estimate);
        var snapshots = new List<DownloadProgress>();
        foreach (var text in Lines(fixture))
        {
            if (YtDlpProgressParser.Parse(text) is { } parsed && tracker.Apply(parsed) is { } snapshot)
            {
                snapshots.Add(snapshot);
            }
        }

        return snapshots;
    }

    [Theory]
    [InlineData(476_000L)]
    [InlineData(null)]
    public void A_two_stream_download_has_one_steady_progress_bar(long? estimate)
    {
        var snapshots = Replay("dl_video_merge.out", estimate);

        // The bar never goes backwards, even though video and audio each count from zero.
        for (var i = 1; i < snapshots.Count; i++)
        {
            Assert.True(snapshots[i].Percent >= snapshots[i - 1].Percent, $"step {i}: {snapshots[i - 1].Percent} -> {snapshots[i].Percent}");
        }

        // It never claims 100% before the file is saved.
        Assert.All(snapshots, s => Assert.True(s.Percent <= 99));
        Assert.Equal(DownloadStage.Downloading, snapshots[0].Stage);

        var stages = snapshots.Select(s => s.Stage).Distinct().ToList();
        Assert.Equal([DownloadStage.Downloading, DownloadStage.Merging, DownloadStage.PostProcessing], stages);
        Assert.Equal(223779 + 252182, snapshots.Last().DownloadedBytes);
    }

    [Fact]
    public void With_an_estimate_the_bar_reflects_both_streams()
    {
        var snapshots = Replay("dl_video_merge.out", estimate: 476_000);

        // Halfway through the combined bytes the bar is about halfway, not at 100% of the first stream.
        var afterFirstStream = snapshots.First(s => s.DownloadedBytes == 223779 && s.Stage == DownloadStage.Downloading);
        Assert.InRange(afterFirstStream.Percent, 40, 55);
    }

    [Fact]
    public void Speed_and_time_left_come_from_the_latest_line_and_clear_when_downloading_ends()
    {
        var tracker = new ProgressTracker(1_000_000);

        var downloading = tracker.Apply(YtDlpProgressParser.Parse("DWN|downloading|250000|1000000|NA|50000.0|15|NA|NA|137")!)!;
        Assert.Equal(50000, downloading.BytesPerSecond);
        Assert.Equal(TimeSpan.FromSeconds(15), downloading.Eta);
        Assert.Equal(25, downloading.Percent, precision: 3);

        var merging = tracker.Apply(YtDlpProgressParser.Parse("PP|started|Merger")!)!;
        Assert.Null(merging.BytesPerSecond);
        Assert.Null(merging.Eta);
        Assert.Equal(DownloadStage.Merging, merging.Stage);
    }

    [Fact]
    public void Time_left_is_worked_out_from_speed_when_yt_dlp_gives_none()
    {
        var tracker = new ProgressTracker(null);

        var snapshot = tracker.Apply(YtDlpProgressParser.Parse("DWN|downloading|1000|5000|NA|1000.0|NA|NA|NA|18")!)!;

        Assert.Equal(TimeSpan.FromSeconds(4), snapshot.Eta);
    }

    [Fact]
    public void Audio_extraction_is_post_processing_not_merging()
    {
        var snapshots = Replay("dl_audio_extract.out", estimate: 252_182);

        Assert.Equal([DownloadStage.Downloading, DownloadStage.PostProcessing], snapshots.Select(s => s.Stage).Distinct());
        Assert.DoesNotContain(snapshots, s => s.Stage == DownloadStage.Merging);
    }

    [Fact]
    public void Segmented_downloads_without_a_size_extrapolate_from_the_fragment_count()
    {
        var snapshots = Replay("dl_fragments.out", estimate: null);

        // After 5 of 20 fragments, 1,310,720 bytes means roughly 5.2 MB in total.
        var fifth = snapshots.First(s => s.DownloadedBytes == 1_310_720);
        Assert.Equal(5_242_880, fifth.TotalBytes);
        Assert.InRange(fifth.Percent, 24, 26);

        for (var i = 1; i < snapshots.Count; i++)
        {
            Assert.True(snapshots[i].Percent >= snapshots[i - 1].Percent);
        }
    }

    [Fact]
    public void A_line_that_is_not_progress_changes_nothing()
    {
        var tracker = new ProgressTracker(null);
        Assert.Null(tracker.Apply(new FileLine(@"C:\x.mp4")));
        Assert.Equal(DownloadStage.Starting, tracker.Current.Stage);
        Assert.Equal(0, tracker.Current.Percent);
    }
}
