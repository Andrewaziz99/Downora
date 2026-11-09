using Downora.Core.Enums;

namespace Downora.Presentation.Services;

/// <summary>Applies Dark / Light / System and reports the theme actually showing.</summary>
public interface IThemeService
{
    AppTheme Theme { get; }

    /// <summary>True when the effective theme is dark (for System, whatever Windows uses).</summary>
    bool IsDark { get; }

    void Apply(AppTheme theme);

    event EventHandler? EffectiveThemeChanged;
}

/// <summary>Modal prompts. Implemented with real Windows dialogs in the App project.</summary>
public interface IDialogService
{
    /// <summary>Shows a folder picker; null when the user cancels.</summary>
    string? PickFolder(string title, string? initialFolder);

    /// <summary>Asks the user to confirm a destructive action; true when they pick <paramref name="confirmText"/>.</summary>
    Task<bool> ConfirmAsync(string title, string message, string confirmText, string cancelText);
}

/// <summary>Hands files, folders and links to Windows.</summary>
public interface IShellService
{
    void OpenFolder(string folderPath);

    void OpenFile(string filePath);

    void ShowFileInFolder(string filePath);

    void OpenUrl(Uri url);
}

/// <summary>Facts about the running build.</summary>
public interface IAppInfo
{
    string Version { get; }

    string DataDirectory { get; }

    string LogsDirectory { get; }
}

/// <summary>Reads text the user copied (the Paste button).</summary>
public interface IClipboardService
{
    /// <summary>The clipboard text, or null when it holds no text or can't be read right now.</summary>
    string? GetText();
}

/// <summary>Runs work on the UI thread. Downloads report from background threads; screens change only through this.</summary>
public interface IUiThread
{
    /// <summary>Runs <paramref name="action"/> on the UI thread: at once when already there, otherwise queued.</summary>
    void Post(Action action);
}
