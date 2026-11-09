using Downora.Core.Enums;
using Downora.Core.Models;
using Downora.Core.Services;

namespace Downora.Core.Tests;

public class DownloadJobTransitionsTests
{
    private static readonly DateTime Now = new(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);

    private static DownloadJob Job(DownloadStatus status = DownloadStatus.Waiting) => new()
    {
        SourceUrl = "https://example.com/v",
        NormalizedUrl = "https://example.com/v",
        OutputDirectory = @"D:\Media",
        Status = status,
    };

    private static DownloadProgress Progress(DownloadStage stage, double percent = 0, long downloaded = 0, long? total = null) =>
        new(stage, percent, downloaded, total, null, null);

    [Fact]
    public void A_download_moves_through_every_state_to_completed()
    {
        var job = Job();

        DownloadJobTransitions.Start(job, Now);
        Assert.Equal(DownloadStatus.Extracting, job.Status);
        Assert.Equal(Now, job.StartedAtUtc);

        Assert.True(DownloadJobTransitions.ApplyProgress(job, Progress(DownloadStage.Downloading, 10, 100, 1000)));
        Assert.Equal(DownloadStatus.Downloading, job.Status);

        Assert.True(DownloadJobTransitions.ApplyProgress(job, Progress(DownloadStage.Merging, 99, 1000, 1000)));
        Assert.Equal(DownloadStatus.Processing, job.Status);

        DownloadJobTransitions.Complete(job, @"D:\Media\a.mp4", 1234, Now.AddMinutes(1));
        Assert.Equal(DownloadStatus.Completed, job.Status);
        Assert.Equal(@"D:\Media\a.mp4", job.OutputPath);
        Assert.Equal(100, job.ProgressPercent);
        Assert.Equal(1234, job.DownloadedBytes);
        Assert.Equal(1234, job.TotalBytes);
        Assert.Equal(Now.AddMinutes(1), job.CompletedAtUtc);
        Assert.True(job.IsFinished);
    }

    [Fact]
    public void Progress_within_the_same_stage_does_not_report_a_status_change()
    {
        var job = Job();
        DownloadJobTransitions.Start(job, Now);

        Assert.True(DownloadJobTransitions.ApplyProgress(job, Progress(DownloadStage.Downloading, 5)));
        Assert.False(DownloadJobTransitions.ApplyProgress(job, Progress(DownloadStage.Downloading, 6)));
        Assert.False(DownloadJobTransitions.ApplyProgress(job, Progress(DownloadStage.Downloading, 7)));
    }

    [Fact]
    public void Starting_stage_keeps_the_job_in_extracting()
    {
        var job = Job();
        DownloadJobTransitions.Start(job, Now);

        Assert.False(DownloadJobTransitions.ApplyProgress(job, Progress(DownloadStage.Starting)));
        Assert.Equal(DownloadStatus.Extracting, job.Status);
    }

    [Fact]
    public void Status_and_percentage_never_move_backwards()
    {
        var job = Job();
        DownloadJobTransitions.Start(job, Now);
        DownloadJobTransitions.ApplyProgress(job, Progress(DownloadStage.Merging, 99, 900, 1000));

        var changed = DownloadJobTransitions.ApplyProgress(job, Progress(DownloadStage.Downloading, 30, 300, 1000));

        Assert.False(changed);
        Assert.Equal(DownloadStatus.Processing, job.Status);
        Assert.Equal(99, job.ProgressPercent);
        Assert.Equal(900, job.DownloadedBytes);
    }

    [Fact]
    public void Percentages_are_kept_within_zero_and_one_hundred()
    {
        var job = Job();
        DownloadJobTransitions.Start(job, Now);

        DownloadJobTransitions.ApplyProgress(job, Progress(DownloadStage.Downloading, 250));
        Assert.Equal(100, job.ProgressPercent);

        var other = Job();
        DownloadJobTransitions.Start(other, Now);
        DownloadJobTransitions.ApplyProgress(other, Progress(DownloadStage.Downloading, -5));
        Assert.Equal(0, other.ProgressPercent);
    }

    [Fact]
    public void A_better_total_replaces_the_estimate_but_an_unknown_one_does_not_erase_it()
    {
        var job = Job();
        job.TotalBytes = 5000; // the analysis' estimate
        DownloadJobTransitions.Start(job, Now);

        DownloadJobTransitions.ApplyProgress(job, Progress(DownloadStage.Downloading, 1, 10, null));
        Assert.Equal(5000, job.TotalBytes);

        DownloadJobTransitions.ApplyProgress(job, Progress(DownloadStage.Downloading, 2, 20, 4800));
        Assert.Equal(4800, job.TotalBytes);
    }

    [Fact]
    public void A_failure_records_why_and_when()
    {
        var job = Job();
        DownloadJobTransitions.Start(job, Now);

        DownloadJobTransitions.Fail(job, DownloadErrorKind.Network, "HTTP Error 503", Now.AddSeconds(5));

        Assert.Equal(DownloadStatus.Failed, job.Status);
        Assert.Equal(DownloadErrorKind.Network, job.ErrorKind);
        Assert.Equal("HTTP Error 503", job.ErrorMessage);
        Assert.Equal(Now.AddSeconds(5), job.CompletedAtUtc);
        Assert.True(job.IsFinished);
    }

    [Fact]
    public void A_failure_always_has_a_kind_and_a_message_that_fits_the_database()
    {
        var job = Job();
        DownloadJobTransitions.Start(job, Now);

        DownloadJobTransitions.Fail(job, DownloadErrorKind.None, new string('x', 5000), Now);

        Assert.Equal(DownloadErrorKind.Unknown, job.ErrorKind);
        Assert.Equal(2000, job.ErrorMessage!.Length);
    }

    [Fact]
    public void Cancelling_ends_the_job_without_an_error()
    {
        var job = Job();
        DownloadJobTransitions.Start(job, Now);
        DownloadJobTransitions.ApplyProgress(job, Progress(DownloadStage.Downloading, 40, 400, 1000));

        DownloadJobTransitions.Cancel(job, Now.AddSeconds(9));

        Assert.Equal(DownloadStatus.Cancelled, job.Status);
        Assert.Equal(DownloadErrorKind.None, job.ErrorKind);
        Assert.Equal(Now.AddSeconds(9), job.CompletedAtUtc);
        Assert.Equal(40, job.ProgressPercent);
    }

    [Fact]
    public void A_paused_job_is_not_finished_and_can_start_again()
    {
        var job = Job();
        DownloadJobTransitions.Start(job, Now);
        DownloadJobTransitions.ApplyProgress(job, Progress(DownloadStage.Downloading, 40, 400, 1000));

        DownloadJobTransitions.Pause(job);
        Assert.Equal(DownloadStatus.Paused, job.Status);
        Assert.False(job.IsFinished);
        Assert.Null(job.CompletedAtUtc);

        DownloadJobTransitions.Start(job, Now.AddHours(1));
        Assert.Equal(DownloadStatus.Extracting, job.Status);
        Assert.Equal(Now, job.StartedAtUtc); // when it first began
        Assert.Equal(40, job.ProgressPercent);
    }

    [Fact]
    public void A_restart_clears_the_previous_error()
    {
        var job = Job(DownloadStatus.Paused);
        job.ErrorKind = DownloadErrorKind.Network;
        job.ErrorMessage = "old";

        DownloadJobTransitions.Start(job, Now);

        Assert.Equal(DownloadErrorKind.None, job.ErrorKind);
        Assert.Null(job.ErrorMessage);
    }

    [Fact]
    public void A_completed_download_never_changes()
    {
        foreach (var target in Enum.GetValues<DownloadStatus>())
        {
            Assert.False(DownloadJobTransitions.CanMove(DownloadStatus.Completed, target), $"Completed -> {target}");
        }

        var done = Job(DownloadStatus.Completed);
        Assert.Throws<InvalidOperationException>(() => DownloadJobTransitions.Start(done, Now));
        Assert.Throws<InvalidOperationException>(() => DownloadJobTransitions.Cancel(done, Now));
        Assert.Throws<InvalidOperationException>(() => DownloadJobTransitions.Fail(done, DownloadErrorKind.Unknown, null, Now));
        Assert.Throws<InvalidOperationException>(() => DownloadJobTransitions.Pause(done));
        Assert.Throws<InvalidOperationException>(() => DownloadJobTransitions.Retry(done));
        Assert.False(DownloadJobTransitions.ApplyProgress(done, Progress(DownloadStage.Downloading, 50)));
    }

    [Theory]
    [InlineData(DownloadStatus.Failed)]
    [InlineData(DownloadStatus.Cancelled)]
    public void A_failed_or_cancelled_job_can_only_go_back_in_line(DownloadStatus finished)
    {
        foreach (var target in Enum.GetValues<DownloadStatus>())
        {
            Assert.True((target == DownloadStatus.Waiting) == DownloadJobTransitions.CanMove(finished, target), $"{finished} -> {target}");
        }

        Assert.Throws<InvalidOperationException>(() => DownloadJobTransitions.Start(Job(finished), Now));
        Assert.Throws<InvalidOperationException>(() => DownloadJobTransitions.Cancel(Job(finished), Now));
        Assert.Throws<InvalidOperationException>(() => DownloadJobTransitions.Complete(Job(finished), "x", 1, Now));
        Assert.Throws<InvalidOperationException>(() => DownloadJobTransitions.Pause(Job(finished)));
        Assert.False(DownloadJobTransitions.ApplyProgress(Job(finished), Progress(DownloadStage.Downloading, 50)));
    }

    [Fact]
    public void Retrying_a_failed_job_keeps_its_progress_and_clears_the_error()
    {
        var job = Job();
        DownloadJobTransitions.Start(job, Now);
        DownloadJobTransitions.ApplyProgress(job, Progress(DownloadStage.Downloading, 60, 600, 1000));
        DownloadJobTransitions.Fail(job, DownloadErrorKind.Network, "dropped", Now.AddMinutes(1));

        DownloadJobTransitions.Retry(job);

        Assert.Equal(DownloadStatus.Waiting, job.Status);
        Assert.Equal(1, job.RetryCount);
        Assert.Equal(DownloadErrorKind.None, job.ErrorKind);
        Assert.Null(job.ErrorMessage);
        Assert.Null(job.CompletedAtUtc);
        Assert.Equal(60, job.ProgressPercent); // its partial files are still there, so it continues from here
        Assert.Equal(600, job.DownloadedBytes);
    }

    [Fact]
    public void Retrying_a_cancelled_job_starts_from_nothing_because_its_files_were_deleted()
    {
        var job = Job();
        DownloadJobTransitions.Start(job, Now);
        DownloadJobTransitions.ApplyProgress(job, Progress(DownloadStage.Downloading, 60, 600, 1000));
        DownloadJobTransitions.Cancel(job, Now.AddMinutes(1));

        DownloadJobTransitions.Retry(job);

        Assert.Equal(DownloadStatus.Waiting, job.Status);
        Assert.Equal(0, job.ProgressPercent);
        Assert.Equal(0, job.DownloadedBytes);
        Assert.Equal(1, job.RetryCount);
    }

    [Fact]
    public void Resuming_puts_a_paused_job_back_in_line_with_its_progress()
    {
        var job = Job();
        DownloadJobTransitions.Start(job, Now);
        DownloadJobTransitions.ApplyProgress(job, Progress(DownloadStage.Downloading, 40, 400, 1000));
        DownloadJobTransitions.Pause(job);

        DownloadJobTransitions.Resume(job);

        Assert.Equal(DownloadStatus.Waiting, job.Status);
        Assert.Equal(40, job.ProgressPercent);
        Assert.Equal(0, job.RetryCount);
        Assert.Throws<InvalidOperationException>(() => DownloadJobTransitions.Resume(job)); // only a paused job resumes
    }

    [Fact]
    public void A_waiting_job_cannot_complete_without_having_run()
    {
        Assert.False(DownloadJobTransitions.CanMove(DownloadStatus.Waiting, DownloadStatus.Completed));
        Assert.False(DownloadJobTransitions.CanMove(DownloadStatus.Waiting, DownloadStatus.Downloading));
        Assert.Throws<InvalidOperationException>(() => DownloadJobTransitions.Complete(Job(), "x", 1, Now));
    }

    [Fact]
    public void Any_unfinished_job_can_fail_be_cancelled_or_paused()
    {
        foreach (var status in new[] { DownloadStatus.Waiting, DownloadStatus.Extracting, DownloadStatus.Downloading, DownloadStatus.Processing, DownloadStatus.Paused })
        {
            Assert.True(DownloadJobTransitions.CanMove(status, DownloadStatus.Failed), status.ToString());
            Assert.True(DownloadJobTransitions.CanMove(status, DownloadStatus.Cancelled), status.ToString());
            Assert.True(DownloadJobTransitions.CanMove(status, DownloadStatus.Paused), status.ToString());
        }
    }

    [Theory]
    [InlineData(DownloadStage.Starting, DownloadStatus.Extracting)]
    [InlineData(DownloadStage.Downloading, DownloadStatus.Downloading)]
    [InlineData(DownloadStage.Merging, DownloadStatus.Processing)]
    [InlineData(DownloadStage.PostProcessing, DownloadStatus.Processing)]
    public void Each_engine_stage_maps_to_a_job_state(DownloadStage stage, DownloadStatus expected) =>
        Assert.Equal(expected, DownloadJobTransitions.StatusFor(stage));
}
