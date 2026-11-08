using CommunityToolkit.Mvvm.ComponentModel;
using Downora.Core.Enums;
using Downora.Core.Models;
using Downora.Core.Services;
using Downora.Presentation.Localization;

namespace Downora.Presentation.ViewModels;

/// <summary>One download as shown on a queue card, a Home row or a History row.</summary>
public sealed class DownloadItemViewModel
{
    public DownloadItemViewModel(DownloadJob job, Localizer localizer)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(localizer);
        var culture = localizer.Culture;

        Id = job.Id;
        Title = string.IsNullOrWhiteSpace(job.Title) ? job.SourceUrl : job.Title;
        Subtitle = job.Uploader ?? job.Platform.ToString();
        ThumbnailUrl = job.ThumbnailUrl;
        Status = job.Status;
        StatusLabel = localizer[$"Status_{job.Status}"];
        FileType = Formatting.FileType(job.MediaKind, job.Container, job.AudioFormat);
        Quality = job.MediaKind == MediaKind.Audio && job.AudioBitrateKbps is { } kbps
            ? $"{kbps} kbps"
            : Formatting.Quality(job.QualityPreset);
        Progress = Math.Clamp(job.ProgressPercent, 0, 100);
        IsIndeterminate = job.Status is DownloadStatus.Extracting or DownloadStatus.Processing;
        SizeText = job.TotalBytes is > 0 and var total
            ? localizer.Format("Queue_SizeOf", Formatting.Bytes(job.DownloadedBytes, culture), Formatting.Bytes(total, culture))
            : job.DownloadedBytes > 0 ? Formatting.Bytes(job.DownloadedBytes, culture) : string.Empty;
        ErrorMessage = job.Status == DownloadStatus.Failed ? job.ErrorMessage : null;
        OutputPath = job.OutputPath;
        OutputDirectory = job.OutputDirectory;
        FinishedText = (job.CompletedAtUtc ?? job.CreatedAtUtc).ToLocalTime().ToString("g", culture);
    }

    public Guid Id { get; }
    public string Title { get; }
    public string Subtitle { get; }
    public string? ThumbnailUrl { get; }
    public DownloadStatus Status { get; }
    public string StatusLabel { get; }
    public string FileType { get; }
    public string Quality { get; }
    public double Progress { get; }
    public bool IsIndeterminate { get; }
    public string SizeText { get; }
    public string? ErrorMessage { get; }
    public string? OutputPath { get; }
    public string OutputDirectory { get; }
    public string FinishedText { get; }
    public bool HasOutputFile => !string.IsNullOrEmpty(OutputPath);
}

/// <summary>One file in the media library.</summary>
public sealed class MediaItemViewModel
{
    public MediaItemViewModel(MediaItem item, Localizer localizer)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(localizer);
        var culture = localizer.Culture;

        Id = item.Id;
        FilePath = item.FilePath;
        Title = string.IsNullOrWhiteSpace(item.Title) ? Path.GetFileNameWithoutExtension(item.FilePath) : item.Title;
        Subtitle = item.Artist ?? string.Empty;
        IsAudio = item.MediaKind == MediaKind.Audio;
        var extension = Path.GetExtension(item.FilePath).TrimStart('.');
        FileType = extension.Length > 0 ? extension.ToUpperInvariant() : (IsAudio ? "AUDIO" : "VIDEO");
        Resolution = item.Height is { } h && !IsAudio ? $"{h}p" : item.BitrateKbps is { } kbps ? $"{kbps} kbps" : null;
        DurationText = Formatting.Duration(item.Duration, culture);
        SizeText = Formatting.Bytes(item.FileSizeBytes, culture);
        ThumbnailPath = item.ThumbnailPath;
        IsMissing = item.IsMissing;
        AddedText = item.AddedAtUtc.ToLocalTime().ToString("d", culture);
    }

    public Guid Id { get; }
    public string FilePath { get; }
    public string Title { get; }
    public string Subtitle { get; }
    public bool IsAudio { get; }
    public string FileType { get; }
    public string? Resolution { get; }
    public string? DurationText { get; }
    public string SizeText { get; }
    public string? ThumbnailPath { get; }
    public bool IsMissing { get; }
    public string AddedText { get; }
}

/// <summary>A selectable choice whose label follows the UI language (theme, language, sort and filter pickers).</summary>
public sealed partial class OptionViewModel<T> : ObservableObject
{
    private readonly Localizer _localizer;

    public OptionViewModel(T value, string labelKey, Localizer localizer)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        Value = value;
        LabelKey = labelKey;
        _localizer = localizer;
        _localizer.LanguageChanged += (_, _) => OnPropertyChanged(nameof(Label));
    }

    public T Value { get; }

    public string LabelKey { get; }

    public string Label => _localizer[LabelKey];

    [ObservableProperty]
    public partial int? Count { get; set; }

    public override string ToString() => Label;
}
