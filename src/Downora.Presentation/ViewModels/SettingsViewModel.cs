using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Downora.Core.Enums;
using Downora.Core.Interfaces;
using Downora.Core.Models;
using Downora.Core.Services;
using Downora.Presentation.Localization;
using Downora.Presentation.Services;
using Microsoft.Extensions.Logging;

namespace Downora.Presentation.ViewModels;

/// <summary>
/// Settings that already do something. Each change applies and saves immediately (Windows Settings style).
/// Download defaults, startup and tool options are added with the features that use them.
/// </summary>
public sealed partial class SettingsViewModel : Navigation.PageViewModel
{
    private readonly ISettingsService _settings;
    private readonly IThemeService _theme;
    private readonly IDialogService _dialogs;
    private readonly IShellService _shell;
    private readonly IAppInfo _app;
    private readonly IToolManager _tools;
    private bool _loading;

    public SettingsViewModel(
        ISettingsService settings,
        IThemeService theme,
        IDialogService dialogs,
        IShellService shell,
        IAppInfo app,
        IToolManager tools,
        Localizer localizer,
        ILogger<SettingsViewModel> logger)
        : base(localizer, logger)
    {
        _tools = tools;
        ToolRows =
        [
            new("YtDlp", [ToolKind.YtDlp], tools, localizer),
            new("Ffmpeg", [ToolKind.Ffmpeg, ToolKind.Ffprobe], tools, localizer),
            new("Deno", [ToolKind.Deno], tools, localizer),
        ];

        // Picking the initial options below must not apply or save anything.
        _loading = true;
        _settings = settings;
        _theme = theme;
        _dialogs = dialogs;
        _shell = shell;
        _app = app;

        ThemeOptions =
        [
            new(AppTheme.System, "Theme_System", localizer),
            new(AppTheme.Dark, "Theme_Dark", localizer),
            new(AppTheme.Light, "Theme_Light", localizer),
        ];
        LanguageOptions =
        [
            new(AppLanguage.English, "Language_English", localizer),
            new(AppLanguage.Arabic, "Language_Arabic", localizer),
        ];
        BrowserOptions =
        [
            new(CookieBrowsers.Edge, "Browser_Edge", localizer),
            new(CookieBrowsers.Chrome, "Browser_Chrome", localizer),
            new(CookieBrowsers.Firefox, "Browser_Firefox", localizer),
            new(CookieBrowsers.Brave, "Browser_Brave", localizer),
        ];
        SelectedTheme = ThemeOptions[0];
        SelectedLanguage = LanguageOptions[0];
        SelectedBrowser = BrowserOptions[0];
        _loading = false;
    }

    public IReadOnlyList<ToolRowViewModel> ToolRows { get; }

    public IReadOnlyList<OptionViewModel<AppTheme>> ThemeOptions { get; }

    public IReadOnlyList<OptionViewModel<AppLanguage>> LanguageOptions { get; }

    public IReadOnlyList<OptionViewModel<string>> BrowserOptions { get; }

    /// <summary>Opt-in: let yt-dlp use the sign-in of the chosen browser. Off until the user turns it on.</summary>
    [ObservableProperty]
    public partial bool UseBrowserSession { get; set; }

    [ObservableProperty]
    public partial OptionViewModel<string> SelectedBrowser { get; set; }

    private bool _cookieRevealPending;

    /// <summary>Raised when another screen asks to see the sign-in option, so an open Settings page can scroll to it.</summary>
    public event EventHandler? CookieOptionsRevealRequested;

    /// <summary>Called by screens that explain why the sign-in option would help (the "sign-in required" card).</summary>
    public void RevealCookieOptions()
    {
        _cookieRevealPending = true;
        CookieOptionsRevealRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>True once per request: the view calls it after it has scrolled, so later visits don't scroll again.</summary>
    public bool ConsumeCookieReveal()
    {
        var pending = _cookieRevealPending;
        _cookieRevealPending = false;
        return pending;
    }

    [ObservableProperty]
    public partial OptionViewModel<AppTheme> SelectedTheme { get; set; }

    [ObservableProperty]
    public partial OptionViewModel<AppLanguage> SelectedLanguage { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DownloadDirectoryDisplay))]
    public partial string DownloadDirectory { get; private set; } = string.Empty;

    /// <summary>The folder as shown on screen: kept left-to-right inside the Arabic layout.</summary>
    public string DownloadDirectoryDisplay => Localizer.LeftToRight(DownloadDirectory);

    public string DataDirectory => _app.DataDirectory;

    public string DataDirectoryDisplay => Localizer.LeftToRight(DataDirectory);

    public override async Task OnNavigatedToAsync(CancellationToken cancellationToken)
    {
        LoadSettings();

        // Versions come from running each tool, so they load after the rest of the page is shown.
        await RunSafeAsync(async () =>
        {
            var statuses = await _tools.GetStatusAsync(cancellationToken).ConfigureAwait(true);
            foreach (var row in ToolRows)
            {
                row.Apply(statuses);
            }
        }, showLoading: false).ConfigureAwait(true);
    }

    private void LoadSettings()
    {
        var current = _settings.Current;
        _loading = true;
        try
        {
            SelectedTheme = ThemeOptions.First(o => o.Value == current.Theme);
            SelectedLanguage = LanguageOptions.First(o => o.Value == current.Language);
            DownloadDirectory = current.DownloadDirectory;
            UseBrowserSession = current.UseBrowserSession;
            SelectedBrowser = BrowserOptions.FirstOrDefault(o => o.Value == current.BrowserForCookies) ?? BrowserOptions[0];
            ErrorMessage = null;
        }
        finally
        {
            _loading = false;
        }
    }

    partial void OnSelectedThemeChanged(OptionViewModel<AppTheme> value)
    {
        if (_loading)
        {
            return;
        }

        _theme.Apply(value.Value);
        _ = SaveAsync(s => s.Theme = value.Value);
    }

    partial void OnSelectedLanguageChanged(OptionViewModel<AppLanguage> value)
    {
        if (_loading)
        {
            return;
        }

        L.SetLanguage(value.Value);
        _ = SaveAsync(s => s.Language = value.Value);
    }

    partial void OnUseBrowserSessionChanged(bool value)
    {
        if (_loading)
        {
            return;
        }

        // Turning it on saves the browser shown; turning it off keeps the choice for next time.
        var browser = SelectedBrowser.Value;
        _ = SaveAsync(s =>
        {
            s.UseBrowserSession = value;
            if (value)
            {
                s.BrowserForCookies = browser;
            }
        });
    }

    partial void OnSelectedBrowserChanged(OptionViewModel<string> value)
    {
        if (_loading)
        {
            return;
        }

        _ = SaveAsync(s => s.BrowserForCookies = value.Value);
    }

    [RelayCommand]
    private async Task BrowseFolderAsync()
    {
        var picked = _dialogs.PickFolder(L["Settings_PickFolderTitle"], DownloadDirectory);
        if (string.IsNullOrWhiteSpace(picked) || string.Equals(picked, DownloadDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (await SaveAsync(s => s.DownloadDirectory = picked).ConfigureAwait(true))
        {
            DownloadDirectory = picked;
        }
    }

    [RelayCommand]
    private void OpenDownloadFolder() => _shell.OpenFolder(DownloadDirectory);

    [RelayCommand]
    private void OpenDataFolder() => _shell.OpenFolder(DataDirectory);

    /// <summary>Saves one change. On failure the message is shown and the screen goes back to the stored values.</summary>
    private async Task<bool> SaveAsync(Action<AppSettings> change)
    {
        var updated = _settings.Current;
        change(updated);
        try
        {
            await _settings.SaveAsync(updated).ConfigureAwait(true);
            ErrorMessage = null;
            return true;
        }
        catch (SettingsValidationException ex)
        {
            LoadSettings();
            ErrorMessage = L.Format("Settings_SaveFailed", ex.Message);
            return false;
        }
    }
}

public sealed partial class AboutViewModel : Navigation.PageViewModel
{
    private static readonly Uri RepositoryUrl = new("https://github.com/Andrewaziz99/Downora");

    private readonly IShellService _shell;
    private readonly IAppInfo _app;

    public AboutViewModel(IShellService shell, IAppInfo app, Localizer localizer, ILogger<AboutViewModel> logger)
        : base(localizer, logger)
    {
        _shell = shell;
        _app = app;
        localizer.LanguageChanged += (_, _) => OnPropertyChanged(nameof(VersionText));
    }

    public string VersionText => L.Format("About_Version", _app.Version);

    public IReadOnlyList<ThirdPartyComponent> ThirdParty { get; } =
    [
        new("yt-dlp", "Unlicense", new Uri("https://github.com/yt-dlp/yt-dlp")),
        new("FFmpeg", "LGPL 2.1+", new Uri("https://ffmpeg.org")),
        new("Deno", "MIT", new Uri("https://github.com/denoland/deno")),
        new("WPF UI", "MIT", new Uri("https://github.com/lepoco/wpfui")),
        new("CommunityToolkit.Mvvm", "MIT", new Uri("https://github.com/CommunityToolkit/dotnet")),
        new("Entity Framework Core", "MIT", new Uri("https://github.com/dotnet/efcore")),
        new("Serilog", "Apache 2.0", new Uri("https://github.com/serilog/serilog")),
    ];

    [RelayCommand]
    private void OpenSource() => _shell.OpenUrl(RepositoryUrl);

    [RelayCommand]
    private void OpenLogs() => _shell.OpenFolder(_app.LogsDirectory);

    [RelayCommand]
    private void OpenLink(ThirdPartyComponent? component)
    {
        if (component is not null)
        {
            _shell.OpenUrl(component.Url);
        }
    }
}

public sealed record ThirdPartyComponent(string Name, string License, Uri Url);
