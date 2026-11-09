using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Downora.Core.Enums;
using Downora.Core.Models;
using Downora.Core.Services;
using Downora.Presentation.Localization;
using Downora.Presentation.Services;

namespace Downora.Presentation.ViewModels;

/// <summary>
/// One download as shown on a queue card, a Home row or a History row. Active downloads call <see cref="Apply"/>
/// as the job moves along, and every bound value updates in place.
/// </summary>
public sealed partial class DownloadItemViewModel : ObservableObject
{
    private readonly Localizer _localizer;
    private readonly IDownloadItemActions? _actions;
    private bool _isCancelling;

    public DownloadItemViewModel(DownloadJob job, Localizer localizer, IDownloadItemActions? actions = null)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(localizer);
        _localizer = localizer;
        _actions = actions;

        Id = job.Id;
        Title = string.IsNullOrWhiteSpace(job.Title) ? job.SourceUrl : job.Title;
        Subtitle = job.Uploader ?? job.Platform.ToString();
        ThumbnailUrl = job.ThumbnailUrl;
        FileType = Formatting.FileType(job.MediaKind, job.Container, job.AudioFormat);
        Quality = job.MediaKind == MediaKind.Audio && job.AudioBitrateKbps is { } kbps
            ? $"{kbps} kbps"
            : Formatting.Quality(job.QualityPreset);
        OutputDirectory = job.OutputDirectory;
        QueueOrder = job.QueueOrder;
        Apply(job, null);
    }

    public Guid Id { get; }
    public string Title { get; }
    public string Subtitle { get; }
    public string? ThumbnailUrl { get; }
    public string FileType { get; }
    public string Quality { get; }
    public string OutputDirectory { get; }
    public long QueueOrder { get; }

    [ObservableProperty]
    public partial DownloadStatus Status { get; private set; }

    [ObservableProperty]
    public partial string StatusLabel { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial double Progress { get; private set; }

    [ObservableProperty]
    public partial bool IsIndeterminate { get; private set; }

    /// <summary>"12.3 MB of 40 MB".</summary>
    [ObservableProperty]
    public partial string SizeText { get; private set; } = string.Empty;

    /// <summary>"2.1 MB/s" while downloading, otherwise empty.</summary>
    [ObservableProperty]
    public partial string SpeedText { get; private set; } = string.Empty;

    /// <summary>"0:14 left" while downloading, otherwise empty.</summary>
    [ObservableProperty]
    public partial string EtaText { get; private set; } = string.Empty;

    /// <summary>What is happening when it isn't plain downloading: "Merging video and audio…".</summary>
    [ObservableProperty]
    public partial string StageText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string? ErrorMessage { get; private set; }

    [ObservableProperty]
    public partial DownloadErrorKind ErrorKind { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOutputFile))]
    public partial string? OutputPath { get; private set; }

    [ObservableProperty]
    public partial string FinishedText { get; private set; } = string.Empty;

    /// <summary>The job is not finished, so Cancel does something.</summary>
    [ObservableProperty]
    public partial bool IsActive { get; private set; }

    public bool HasOutputFile => !string.IsNullOrEmpty(OutputPath);

    /// <summary>Updates the card from a job and, while it is running, the engine's latest progress.</summary>
    public void Apply(DownloadJob job, DownloadProgress? progress)
    {
        ArgumentNullException.ThrowIfNull(job);
        var culture = _localizer.Culture;

        Status = job.Status;
        StatusLabel = _localizer[$"Status_{job.Status}"];
        Progress = Math.Clamp(job.ProgressPercent, 0, 100);
        IsIndeterminate = job.Status is DownloadStatus.Extracting or DownloadStatus.Processing;
        SizeText = job.TotalBytes is > 0 and var total
            ? _localizer.Format("Queue_SizeOf", Formatting.Bytes(job.DownloadedBytes, culture), Formatting.Bytes(total, culture))
            : job.DownloadedBytes > 0 ? Formatting.Bytes(job.DownloadedBytes, culture) : string.Empty;

        var downloading = job.Status == DownloadStatus.Downloading && progress is not null;
        SpeedText = downloading && progress!.BytesPerSecond is > 0 and var speed ? Formatting.Speed(speed, culture) : string.Empty;
        EtaText = downloading && progress!.Eta is { } eta && Formatting.Duration(eta, culture) is { } left
            ? _localizer.Format("Queue_Eta", left)
            : string.Empty;
        StageText = _isCancelling && !job.IsFinished
            ? _localizer["Stage_Cancelling"]
            : job.Status switch
            {
                DownloadStatus.Extracting => _localizer["Stage_Starting"],
                DownloadStatus.Processing when progress?.Stage == DownloadStage.PostProcessing => _localizer["Stage_PostProcessing"],
                DownloadStatus.Processing => _localizer["Stage_Merging"],
                _ => string.Empty,
            };

        ErrorKind = job.Status == DownloadStatus.Failed ? job.ErrorKind : DownloadErrorKind.None;
        ErrorMessage = job.Status == DownloadStatus.Failed ? FailureText(job) : null;
        OutputPath = job.OutputPath;
        FinishedText = (job.CompletedAtUtc ?? job.CreatedAtUtc).ToLocalTime().ToString("g", culture);
        IsActive = !job.IsFinished;

        CancelCommand.NotifyCanExecuteChanged();
        ShowFileCommand.NotifyCanExecuteChanged();
    }

    /// <summary>A known cause gets its plain-language title; otherwise the tool's own message, which is all there is.</summary>
    private string FailureText(DownloadJob job) =>
        job.ErrorKind is DownloadErrorKind.None or DownloadErrorKind.Unknown
            ? job.ErrorMessage is { Length: > 0 } message ? message : _localizer["Error_Unknown_Title"]
            : _localizer[$"Error_{job.ErrorKind}_Title"];

    private bool CanCancel() => IsActive && !_isCancelling && _actions is not null;

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private async Task CancelAsync()
    {
        _isCancelling = true;
        StageText = _localizer["Stage_Cancelling"];
        CancelCommand.NotifyCanExecuteChanged();
        await _actions!.CancelAsync(Id).ConfigureAwait(true);
    }

    private bool CanOpen() => _actions is not null;

    [RelayCommand(CanExecute = nameof(CanOpen))]
    private void OpenFolder() => _actions!.OpenFolder(OutputDirectory);

    private bool CanShowFile() => _actions is not null && HasOutputFile;

    [RelayCommand(CanExecute = nameof(CanShowFile))]
    private void ShowFile() => _actions!.ShowFile(OutputPath!, OutputDirectory);
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
