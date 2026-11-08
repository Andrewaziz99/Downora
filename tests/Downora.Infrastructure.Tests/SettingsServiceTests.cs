using Downora.Core.Enums;
using Downora.Core.Interfaces;
using Downora.Core.Models;
using Downora.Core.Services;

namespace Downora.Infrastructure.Tests;

public class SettingsServiceTests
{
    [Fact]
    public async Task First_load_creates_defaults_pointing_at_the_default_download_folder()
    {
        await using var env = await TestEnvironment.StartAsync();
        var service = env.Get<ISettingsService>();

        await service.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(env.Paths.DefaultDownloadDirectory, service.Current.DownloadDirectory);
        Assert.Equal(3, service.Current.MaxConcurrentDownloads);
        Assert.Equal(AppTheme.System, service.Current.Theme);
    }

    [Fact]
    public async Task Current_before_load_explains_what_to_do()
    {
        await using var env = await TestEnvironment.StartAsync();

        var error = Assert.Throws<InvalidOperationException>(() => env.Get<ISettingsService>().Current);
        Assert.Contains("LoadAsync", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Saved_settings_survive_a_restart()
    {
        var ct = TestContext.Current.CancellationToken;
        var env = await TestEnvironment.StartAsync();
        var service = env.Get<ISettingsService>();
        await service.LoadAsync(ct);

        var edited = service.Current;
        edited.MaxConcurrentDownloads = 5;
        edited.Theme = AppTheme.Light;
        edited.Language = AppLanguage.Arabic;
        edited.PreferredAudioFormat = AudioFormat.M4a;
        edited.DefaultAudioBitrateKbps = 256;
        await service.SaveAsync(edited, ct);

        await using var restarted = await env.RestartAsync();
        var reloaded = restarted.Get<ISettingsService>();
        await reloaded.LoadAsync(ct);

        Assert.Equal(5, reloaded.Current.MaxConcurrentDownloads);
        Assert.Equal(AppTheme.Light, reloaded.Current.Theme);
        Assert.Equal(AppLanguage.Arabic, reloaded.Current.Language);
        Assert.Equal(AudioFormat.M4a, reloaded.Current.PreferredAudioFormat);
        Assert.Equal(256, reloaded.Current.DefaultAudioBitrateKbps);
    }

    [Fact]
    public async Task Invalid_settings_are_rejected_and_nothing_changes()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var env = await TestEnvironment.StartAsync();
        var service = env.Get<ISettingsService>();
        await service.LoadAsync(ct);

        var edited = service.Current;
        edited.MaxConcurrentDownloads = 99;

        var error = await Assert.ThrowsAsync<SettingsValidationException>(() => service.SaveAsync(edited, ct));
        Assert.Single(error.Errors);
        Assert.Equal(3, service.Current.MaxConcurrentDownloads);
    }

    [Fact]
    public async Task Current_returns_a_copy_so_edits_need_an_explicit_save()
    {
        await using var env = await TestEnvironment.StartAsync();
        var service = env.Get<ISettingsService>();
        await service.LoadAsync(TestContext.Current.CancellationToken);

        service.Current.MaxConcurrentDownloads = 9;

        Assert.Equal(3, service.Current.MaxConcurrentDownloads);
    }

    [Fact]
    public async Task Save_raises_Changed_with_the_new_values()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var env = await TestEnvironment.StartAsync();
        var service = env.Get<ISettingsService>();
        await service.LoadAsync(ct);
        AppSettings? received = null;
        service.Changed += (_, s) => received = s;

        var edited = service.Current;
        edited.AutoAddToQueue = true;
        await service.SaveAsync(edited, ct);

        Assert.NotNull(received);
        Assert.True(received.AutoAddToQueue);
    }
}
