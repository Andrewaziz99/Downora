using Downora.Core.Models;

namespace Downora.Infrastructure.Downloads;

/// <summary>
/// Combines yt-dlp's per-stream progress lines into one overall figure. A video download is usually two streams
/// (video, then audio) that each count 0–100%, and the bar must not run twice. Pure and single-threaded.
/// </summary>
internal sealed class ProgressTracker(long? estimatedBytes)
{
    /// <summary>Until the download really ends the bar stops short of 100, so "100%" always means a finished file.</summary>
    private const double DownloadingCeiling = 99;

    private readonly Dictionary<string, StreamProgress> _streams = [];
    private DownloadStage _stage = DownloadStage.Starting;
    private double _percent;
    private double? _speed;
    private int? _etaSeconds;

    public DownloadProgress Current => Snapshot();

    /// <summary>Applies a parsed line. Returns the new snapshot, or null when the line changes nothing (a file path, for instance).</summary>
    public DownloadProgress? Apply(YtDlpLine line)
    {
        ArgumentNullException.ThrowIfNull(line);
        switch (line)
        {
            case DownloadLine download:
                ApplyDownload(download);
                return Snapshot();

            case PostProcessLine { IsStarted: true } step:
                // Joining streams is the slow step worth naming; moving the file and the like is quick.
                _stage = step.Processor.Contains("Merger", StringComparison.OrdinalIgnoreCase)
                    ? DownloadStage.Merging
                    : DownloadStage.PostProcessing;
                _speed = null;
                _etaSeconds = null;
                _percent = Math.Max(_percent, DownloadingCeiling);
                return Snapshot();

            default:
                return null;
        }
    }

    private void ApplyDownload(DownloadLine line)
    {
        if (_stage == DownloadStage.Starting)
        {
            _stage = DownloadStage.Downloading;
        }

        var total = line.TotalBytes ?? ExtrapolateFromFragments(line);
        var stream = new StreamProgress(line.IsFinished && total is not null ? total.Value : line.DownloadedBytes, total);
        _streams[line.StreamId] = stream;
        _speed = line.IsFinished ? null : line.BytesPerSecond;
        _etaSeconds = line.EtaSeconds;

        var downloaded = _streams.Values.Sum(s => s.Downloaded);
        var denominator = Denominator();
        if (denominator > 0)
        {
            // Never move backwards: when a second stream appears the known total grows.
            _percent = Math.Max(_percent, Math.Min(DownloadingCeiling, downloaded * 100.0 / denominator));
        }
    }

    /// <summary>Segmented (HLS/DASH) downloads may not know their size, only how many fragments there are.</summary>
    private static long? ExtrapolateFromFragments(DownloadLine line) =>
        line is { FragmentCount: > 0, FragmentIndex: > 0, DownloadedBytes: > 0 }
            ? line.DownloadedBytes * line.FragmentCount.Value / line.FragmentIndex.Value
            : null;

    /// <summary>The total to measure against: the analysis' estimate when it is the larger, otherwise what the streams report.</summary>
    private long Denominator()
    {
        var known = _streams.Values.Sum(s => s.Total ?? s.Downloaded);
        return estimatedBytes is > 0 ? Math.Max(estimatedBytes.Value, known) : known;
    }

    private DownloadProgress Snapshot()
    {
        var downloaded = _streams.Values.Sum(s => s.Downloaded);
        var denominator = Denominator();
        long? total = denominator > 0 ? denominator : null;

        TimeSpan? eta = _etaSeconds is { } seconds
            ? TimeSpan.FromSeconds(seconds)
            : _speed is > 0 && total is { } t && t > downloaded
                ? TimeSpan.FromSeconds((t - downloaded) / _speed.Value)
                : null;

        return new DownloadProgress(_stage, _percent, downloaded, total, _speed, eta);
    }

    private readonly record struct StreamProgress(long Downloaded, long? Total);
}
