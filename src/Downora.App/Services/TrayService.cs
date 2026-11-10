using System.Drawing;
using System.Windows;
using Downora.Core.Enums;
using Downora.Core.Interfaces;
using Downora.Presentation.Localization;
using Downora.Presentation.Services;
using Microsoft.Extensions.Logging;
using Forms = System.Windows.Forms;

namespace Downora.App.Services;

/// <summary>The notification-area icon: open the window, pause or resume everything, exit; its tooltip shows what is downloading.</summary>
internal sealed partial class TrayService : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly Forms.ContextMenuStrip _menu = new();
    private readonly Forms.ToolStripMenuItem _open = new();
    private readonly Forms.ToolStripMenuItem _pauseAll = new();
    private readonly Forms.ToolStripMenuItem _resumeAll = new();
    private readonly Forms.ToolStripMenuItem _exit = new();
    private readonly Localizer _localizer;
    private readonly IDownloadJobService _downloads;
    private readonly IDownloadJobQueries _queries;
    private readonly WindowLifecycle _lifecycle;
    private readonly ILogger<TrayService> _logger;

    public TrayService(
        Localizer localizer,
        IDownloadJobService downloads,
        IDownloadJobQueries queries,
        DownloadFeed feed,
        WindowLifecycle lifecycle,
        ILogger<TrayService> logger)
    {
        ArgumentNullException.ThrowIfNull(feed);
        _localizer = localizer;
        _downloads = downloads;
        _queries = queries;
        _lifecycle = lifecycle;
        _logger = logger;

        _open.Click += (_, _) => _lifecycle.Show();
        _pauseAll.Click += async (_, _) => await RunAsync(() => _downloads.PauseAllAsync());
        _resumeAll.Click += async (_, _) => await RunAsync(() => _downloads.ResumeAllAsync());
        _exit.Click += (_, _) => _lifecycle.Exit();
        _menu.Items.AddRange([_open, _pauseAll, _resumeAll, new Forms.ToolStripSeparator(), _exit]);
        _open.Font = new Font(_open.Font, System.Drawing.FontStyle.Bold);

        _icon = new Forms.NotifyIcon
        {
            Icon = LoadIcon(),
            ContextMenuStrip = _menu,
            Text = "Downora",
            Visible = true,
        };
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left)
            {
                _lifecycle.Show();
            }
        };

        ApplyLanguage();
        _localizer.LanguageChanged += (_, _) => ApplyLanguage();
        feed.Updated += (_, e) =>
        {
            if (e.StatusChanged)
            {
                _ = RefreshTooltipAsync();
            }
        };
        _ = RefreshTooltipAsync();
    }

    public void Dispose()
    {
        // Without this the icon lingers in the tray until the mouse passes over it.
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
    }

    private static Icon LoadIcon()
    {
        var resource = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/downora.ico"));
        return resource is null ? SystemIcons.Application : new Icon(resource.Stream);
    }

    private void ApplyLanguage()
    {
        _open.Text = _localizer["Tray_Open"];
        _pauseAll.Text = _localizer["Tray_PauseAll"];
        _resumeAll.Text = _localizer["Tray_ResumeAll"];
        _exit.Text = _localizer["Tray_Exit"];
        _menu.RightToLeft = _localizer.IsRightToLeft ? Forms.RightToLeft.Yes : Forms.RightToLeft.No;
    }

    private async Task RefreshTooltipAsync()
    {
        try
        {
            var counts = await _queries.CountByStatusAsync().ConfigureAwait(true);
            var running = counts.Where(c => c.Key is DownloadStatus.Extracting or DownloadStatus.Downloading or DownloadStatus.Processing).Sum(c => c.Value);

            // NotifyIcon.Text is limited to 63 characters.
            var text = running > 0 ? _localizer.Format("Tray_Active", running.ToString("N0", _localizer.Culture)) : "Downora";
            _icon.Text = text.Length > 63 ? text[..63] : text;
        }
#pragma warning disable CA1031 // A tooltip is not worth a crash.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogTooltipFailed(ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Could not refresh the tray tooltip")]
    private partial void LogTooltipFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "A tray menu action failed")]
    private partial void LogActionFailed(Exception exception);

    private async Task RunAsync(Func<Task> action)
    {
        try
        {
            await action().ConfigureAwait(true);
        }
#pragma warning disable CA1031 // A tray click must never take the app down.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogActionFailed(ex);
        }
    }
}
