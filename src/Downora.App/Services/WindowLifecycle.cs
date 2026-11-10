using System.ComponentModel;
using System.Windows;
using Downora.Core.Interfaces;

namespace Downora.App.Services;

/// <summary>Showing, hiding and really closing the main window, so "close to tray" and the tray menu agree.</summary>
internal sealed class WindowLifecycle(ISettingsService settings)
{
    private Window? _window;
    private volatile bool _exiting;

    public void Attach(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        _window = window;
        window.Closing += OnClosing;
    }

    /// <summary>Brings the window back from the tray or the taskbar.</summary>
    public void Show()
    {
        if (_window is not { } window)
        {
            return;
        }

        if (!window.IsVisible)
        {
            window.Show();
        }

        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Activate();
    }

    /// <summary>Quits for real, whatever "close to tray" says. Running downloads are paused and kept, as when closing the app.</summary>
    public void Exit()
    {
        MarkExiting();
        Application.Current.Shutdown();
    }

    /// <summary>Windows is signing out or shutting down: the window must be allowed to close.</summary>
    public void MarkExiting() => _exiting = true;

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_exiting && settings.Current.CloseToTray)
        {
            e.Cancel = true;
            _window?.Hide();
        }
    }
}
