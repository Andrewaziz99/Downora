using System.IO;
using System.Windows;
using Downora.Presentation.Services;
using Microsoft.Win32;

namespace Downora.App.Services;

internal sealed class DialogService : IDialogService
{
    public string? PickFolder(string title, string? initialFolder)
    {
        var dialog = new OpenFolderDialog
        {
            Title = title,
            Multiselect = false,
            InitialDirectory = initialFolder is { Length: > 0 } && Directory.Exists(initialFolder) ? initialFolder : string.Empty,
        };

        return dialog.ShowDialog(Application.Current.MainWindow) == true ? dialog.FolderName : null;
    }

    public async Task<bool> ConfirmAsync(string title, string message, string confirmText, string cancelText)
    {
        var box = new Wpf.Ui.Controls.MessageBox
        {
            Title = title,
            Content = message,
            PrimaryButtonText = confirmText,
            CloseButtonText = cancelText,
            IsSecondaryButtonEnabled = false,
            Owner = Application.Current.MainWindow,
            FlowDirection = Application.Current.MainWindow?.FlowDirection ?? FlowDirection.LeftToRight,
        };

        var result = await box.ShowDialogAsync().ConfigureAwait(true);
        return result == Wpf.Ui.Controls.MessageBoxResult.Primary;
    }
}

internal sealed class AppInfoProvider(Core.Interfaces.IAppPaths paths) : IAppInfo
{
    public string Version => AppInfo.Version;

    public string DataDirectory => paths.DataDirectory;

    public string LogsDirectory => paths.LogsDirectory;
}
