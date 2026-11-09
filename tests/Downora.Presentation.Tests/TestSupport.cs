using Downora.Core.Enums;
using Downora.Core.Interfaces;
using Downora.Core.Models;
using Downora.Presentation.Localization;
using Downora.Presentation.Services;
using NSubstitute;

namespace Downora.Presentation.Tests;

/// <summary>Tests that change the UI language touch process-wide culture, so they never run in parallel with each other.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class CultureSensitive
{
    public const string Name = "Culture-sensitive";
}

/// <summary>An in-memory settings store with the real validation rules.</summary>
internal sealed class FakeSettings : ISettingsService
{
    private AppSettings _current = AppSettings.CreateDefault(Path.GetFullPath(Path.Combine(Path.GetTempPath(), "Downora")));

    public AppSettings Current => _current.Clone();

    public int SaveCount { get; private set; }

    public event EventHandler<AppSettings>? Changed;

    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        var errors = Core.Services.AppSettingsValidator.Validate(settings);
        if (errors.Count > 0)
        {
            throw new Core.Services.SettingsValidationException(errors);
        }

        _current = settings.Clone();
        SaveCount++;
        Changed?.Invoke(this, settings.Clone());
        return Task.CompletedTask;
    }
}

/// <summary>Runs UI work immediately, so tests don't need a dispatcher.</summary>
internal sealed class InlineUiThread : IUiThread
{
    public void Post(Action action) => action();
}

internal sealed class FakeTheme : IThemeService
{
    public AppTheme Theme { get; private set; } = AppTheme.Dark;

    public bool IsDark { get; private set; } = true;

    public event EventHandler? EffectiveThemeChanged;

    public void Apply(AppTheme theme)
    {
        Theme = theme;
        IsDark = theme != AppTheme.Light;
        EffectiveThemeChanged?.Invoke(this, EventArgs.Empty);
    }
}

internal static class Fakes
{
    public static IAppInfo AppInfo()
    {
        var app = Substitute.For<IAppInfo>();
        app.Version.Returns("1.2.3");
        app.DataDirectory.Returns("/data/Downora");
        app.LogsDirectory.Returns("/data/Downora/logs");
        return app;
    }

    public static DownloadJob Job(string title, DownloadStatus status, long downloaded = 0, long? total = null) => new()
    {
        SourceUrl = "https://example.com/" + title,
        NormalizedUrl = "https://example.com/" + title,
        Title = title,
        OutputDirectory = "/downloads",
        Status = status,
        DownloadedBytes = downloaded,
        TotalBytes = total,
        CreatedAtUtc = new DateTime(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc),
    };
}
