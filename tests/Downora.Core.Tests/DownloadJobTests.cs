using Downora.Core.Enums;
using Downora.Core.Models;

namespace Downora.Core.Tests;

public class DownloadJobTests
{
    [Theory]
    [InlineData(DownloadStatus.Completed, true)]
    [InlineData(DownloadStatus.Failed, true)]
    [InlineData(DownloadStatus.Cancelled, true)]
    [InlineData(DownloadStatus.Waiting, false)]
    [InlineData(DownloadStatus.Extracting, false)]
    [InlineData(DownloadStatus.Downloading, false)]
    [InlineData(DownloadStatus.Processing, false)]
    [InlineData(DownloadStatus.Paused, false)]
    public void IsFinished_is_true_only_for_final_states(DownloadStatus status, bool finished)
    {
        var job = new DownloadJob
        {
            SourceUrl = "https://example.com/v",
            NormalizedUrl = "https://example.com/v",
            OutputDirectory = "C:\\Downloads",
            Status = status,
        };

        Assert.Equal(finished, job.IsFinished);
    }
}
