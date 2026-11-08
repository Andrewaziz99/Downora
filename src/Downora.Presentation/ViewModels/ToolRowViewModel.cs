using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Downora.Core.Enums;
using Downora.Core.Interfaces;
using Downora.Presentation.Localization;

namespace Downora.Presentation.ViewModels;

/// <summary>A row in Settings › Tools: what's installed, and Install / Check for updates.</summary>
public sealed partial class ToolRowViewModel : ObservableObject
{
    private readonly IToolManager _tools;
    private readonly Localizer _l;

    /// <param name="tools">Every executable this row stands for (FFmpeg + FFprobe share one row). The first one's version is shown.</param>
    public ToolRowViewModel(string key, IReadOnlyList<ToolKind> tools, IToolManager toolManager, Localizer localizer)
    {
        ArgumentNullException.ThrowIfNull(tools);
        ArgumentNullException.ThrowIfNull(localizer);
        Tools = tools;
        _tools = toolManager;
        _l = localizer;
        TitleKey = $"Tool_{key}";
        DescriptionKey = $"Tool_{key}Desc";
        CanUpdate = tools[0] == ToolKind.YtDlp;
        StatusText = localizer["Tool_Checking"];
        localizer.LanguageChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(Description));
        };
    }

    public string Title => _l[TitleKey];

    public string Description => _l[DescriptionKey];

    public IReadOnlyList<ToolKind> Tools { get; }

    public string TitleKey { get; }

    public string DescriptionKey { get; }

    /// <summary>Only yt-dlp updates itself; the others are updated with Downora.</summary>
    public bool CanUpdate { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowInstall), nameof(ShowUpdate))]
    public partial bool IsInstalled { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowInstall), nameof(ShowUpdate))]
    [NotifyCanExecuteChangedFor(nameof(InstallCommand), nameof(UpdateCommand))]
    public partial bool IsBusy { get; private set; }

    [ObservableProperty]
    public partial string StatusText { get; private set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; private set; }

    public bool ShowInstall => !IsInstalled;

    public bool ShowUpdate => IsInstalled && CanUpdate;

    public void Apply(IReadOnlyList<ToolStatus> statuses)
    {
        ArgumentNullException.ThrowIfNull(statuses);
        var mine = statuses.Where(s => Tools.Contains(s.Tool)).ToList();
        IsInstalled = mine.Count == Tools.Count && mine.All(s => s.IsInstalled);
        var version = mine.FirstOrDefault(s => s.Tool == Tools[0])?.Version;
        StatusText = !IsInstalled
            ? _l["Tool_NotInstalled"]
            : version is null ? _l["Tool_InstalledNoVersion"] : _l.Format("Tool_Version", version);
    }

    private bool NotBusy() => !IsBusy;

    [RelayCommand(CanExecute = nameof(NotBusy))]
    private async Task InstallAsync()
    {
        ErrorMessage = null;
        IsBusy = true;
        try
        {
            var name = _l[TitleKey];
            var progress = new Progress<ToolInstallProgress>(p =>
                StatusText = _l.Format("Downloader_Installing", name, p.Fraction is { } f ? f.ToString("P0", _l.Culture) : string.Empty));
            await _tools.InstallAsync(Tools[0], progress).ConfigureAwait(true);
        }
        catch (ToolInstallException ex)
        {
            ErrorMessage = _l.Format("Error_InstallFailed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }

        Apply(await _tools.GetStatusAsync().ConfigureAwait(true));
    }

    [RelayCommand(CanExecute = nameof(NotBusy))]
    private async Task UpdateAsync()
    {
        ErrorMessage = null;
        IsBusy = true;
        StatusText = _l["Tool_Checking"];
        try
        {
            await _tools.UpdateYtDlpAsync().ConfigureAwait(true);
        }
        catch (ToolInstallException ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }

        Apply(await _tools.GetStatusAsync().ConfigureAwait(true));
    }
}
