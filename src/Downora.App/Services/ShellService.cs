using System.Diagnostics;
using System.IO;
using Downora.Core.Services;
using Downora.Presentation.Services;

namespace Downora.App.Services;

/// <summary>Opens files, folders and links with Windows. Paths are passed as arguments, never through a command shell.</summary>
internal sealed class ShellService : IShellService
{
    /// <summary>The full path, so a look-alike explorer.exe next to Downora or in the current folder can never be picked up.</summary>
    private static readonly string Explorer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");

    public void OpenFolder(string folderPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
        Directory.CreateDirectory(folderPath);
        var startInfo = new ProcessStartInfo(Explorer) { UseShellExecute = false };
        startInfo.ArgumentList.Add(folderPath);
        using var _ = Process.Start(startInfo);
    }

    public void OpenFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("The file no longer exists.", filePath);
        }

        // Anything that isn't a known media type (a program, a script, a shortcut) is shown, never run.
        if (!MediaFileTypes.IsOpenable(filePath))
        {
            ShowFileInFolder(filePath);
            return;
        }

        // UseShellExecute opens the file with its associated app (the default player).
        using var _ = Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
    }

    public void ShowFileInFolder(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var fullPath = Path.GetFullPath(filePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("The file no longer exists.", fullPath);
        }

        // Explorer needs the quotes inside the /select switch; Windows paths can't contain quotes, so this is safe.
        var startInfo = new ProcessStartInfo(Explorer, $"/select,\"{fullPath}\"") { UseShellExecute = false };
        using var _ = Process.Start(startInfo);
    }

    public void OpenUrl(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
        if (!url.IsAbsoluteUri || (url.Scheme != Uri.UriSchemeHttps && url.Scheme != Uri.UriSchemeHttp))
        {
            throw new ArgumentException("Only web links can be opened.", nameof(url));
        }

        using var _ = Process.Start(new ProcessStartInfo(url.AbsoluteUri) { UseShellExecute = true });
    }
}
