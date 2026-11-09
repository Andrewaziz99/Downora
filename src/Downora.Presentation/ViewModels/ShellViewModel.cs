using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Downora.Core.Enums;
using Downora.Core.Interfaces;
using Downora.Core.Services;
using Downora.Presentation.Localization;
using Downora.Presentation.Navigation;
using Downora.Presentation.Services;
using Microsoft.Extensions.Logging;

namespace Downora.Presentation.ViewModels;

/// <summary>A sidebar entry. <see cref="Icon"/> is a Fluent System Icons name.</summary>
public sealed partial class NavItem : ObservableObject
{
    private readonly Localizer _localizer;

    public NavItem(string labelKey, string icon, Type pageType, Localizer localizer)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        LabelKey = labelKey;
        Icon = icon;
        PageType = pageType;
        _localizer = localizer;
        _localizer.LanguageChanged += (_, _) => OnPropertyChanged(nameof(Label));
    }

    public string LabelKey { get; }
    public string Icon { get; }
    public Type PageType { get; }
    public string Label => _localizer[LabelKey];

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>Count shown as a badge (queue only); 0 hides it.</summary>
    [ObservableProperty]
    public partial int Badge { get; set; }
}

public sealed partial class ShellViewModel : ObservableObject
{
    private readonly INavigationService _navigation;
    private readonly IThemeService _theme;
    private readonly ISettingsService _settings;
    private readonly IDiskSpaceService _disk;
    private readonly IDownloadJobQueries _jobs;
    private readonly Localizer _localizer;
    private readonly ILogger<ShellViewModel> _logger;

    public ShellViewModel(
        INavigationService navigation,
        IThemeService theme,
        ISettingsService settings,
        IDiskSpaceService disk,
        IDownloadJobQueries jobs,
        DownloadFeed feed,
        Localizer localizer,
        ILogger<ShellViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(feed);
        _navigation = navigation;
        _theme = theme;
        _settings = settings;
        _disk = disk;
        _jobs = jobs;
        _localizer = localizer;
        _logger = logger;

        Items =
        [
            new("Nav_Home", "Home24", typeof(HomeViewModel), localizer),
            new("Nav_Downloader", "ArrowDownload24", typeof(DownloaderViewModel), localizer),
            new("Nav_Queue", "TextBulletListLtr24", typeof(QueueViewModel), localizer),
            new("Nav_Library", "Library24", typeof(LibraryViewModel), localizer),
            new("Nav_History", "History24", typeof(HistoryViewModel), localizer),
        ];
        FooterItems =
        [
            new("Nav_Settings", "Settings24", typeof(SettingsViewModel), localizer),
            new("Nav_About", "Info24", typeof(AboutViewModel), localizer),
        ];

        _navigation.Navigated += OnNavigated;

        // The sidebar count follows downloads starting and ending, wherever the user is in the app.
        feed.Updated += (_, e) =>
        {
            if (e.StatusChanged)
            {
                _ = RefreshQueueBadgeAsync();
            }
        };
        _theme.EffectiveThemeChanged += (_, _) => OnPropertyChanged(nameof(IsDarkTheme));
        _settings.Changed += (_, _) => RefreshStorage();
        _localizer.LanguageChanged += (_, _) => RefreshStorage();
    }

    public IReadOnlyList<NavItem> Items { get; }

    public IReadOnlyList<NavItem> FooterItems { get; }

    [ObservableProperty]
    public partial PageViewModel? CurrentPage { get; private set; }

    public bool IsDarkTheme => _theme.IsDark;

    [ObservableProperty]
    public partial bool HasStorage { get; private set; }

    [ObservableProperty]
    public partial string StorageTitle { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string StorageDetail { get; private set; } = string.Empty;

    /// <summary>Used share of the download drive, 0–100.</summary>
    [ObservableProperty]
    public partial double StorageUsedPercent { get; private set; }

    public Task StartAsync() => _navigation.NavigateToAsync<HomeViewModel>();

    [RelayCommand]
    private Task NavigateAsync(NavItem? item) =>
        item is null || _navigation.Current?.GetType() == item.PageType
            ? Task.CompletedTask
            : _navigation.NavigateToAsync(item.PageType);

    [RelayCommand]
    private async Task ToggleThemeAsync()
    {
        var target = _theme.IsDark ? AppTheme.Light : AppTheme.Dark;
        _theme.Apply(target);
        OnPropertyChanged(nameof(IsDarkTheme));

        var settings = _settings.Current;
        settings.Theme = target;
        await _settings.SaveAsync(settings).ConfigureAwait(true);
    }

    private void OnNavigated(object? sender, PageViewModel page)
    {
        CurrentPage = page;

        foreach (var item in Items.Concat(FooterItems))
        {
            item.IsSelected = item.PageType == page.GetType();
        }

        RefreshStorage();
        _ = RefreshQueueBadgeAsync();
    }

    private void RefreshStorage()
    {
        var usage = _disk.GetUsage(_settings.Current.DownloadDirectory);
        HasStorage = usage is not null;
        if (usage is null)
        {
            StorageTitle = _localizer["Shell_StorageUnavailable"];
            StorageDetail = string.Empty;
            StorageUsedPercent = 0;
            return;
        }

        var culture = _localizer.Culture;
        StorageTitle = _localizer.Format("Shell_Storage", usage.DriveName);
        StorageDetail = _localizer.Format("Shell_StorageUsed", Formatting.Bytes(usage.FreeBytes, culture), Formatting.Bytes(usage.TotalBytes, culture));
        StorageUsedPercent = Math.Round(usage.UsedFraction * 100, 1);
    }

    private async Task RefreshQueueBadgeAsync()
    {
        try
        {
            var active = await _jobs.GetActiveAsync().ConfigureAwait(true);
            Items.First(i => i.PageType == typeof(QueueViewModel)).Badge = active.Count;
        }
#pragma warning disable CA1031 // The badge is cosmetic; a failed count must not break navigation.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogBadgeFailed(_logger, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not refresh the queue badge")]
    private static partial void LogBadgeFailed(ILogger logger, Exception exception);
}
