using System.Diagnostics;
using System.IO;

namespace Downora.App.Services;

/// <summary>Opens files and folders with Windows Explorer.</summary>
public interface IShellService
{
    void OpenFolder(string folderPath);
    void ShowFileInFolder(string filePath);
}

internal sealed class ShellService : IShellService
{
    public void OpenFolder(string folderPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
        Directory.CreateDirectory(folderPath);
        StartExplorer(folderPath);
    }

    public void ShowFileInFolder(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("The file no longer exists.", filePath);
        }

        StartExplorer($"/select,{filePath}");
    }

    private static void StartExplorer(string argument)
    {
        // ArgumentList passes the path as one argument without going through a shell.
        var startInfo = new ProcessStartInfo("explorer.exe") { UseShellExecute = false };
        startInfo.ArgumentList.Add(argument);
        using var _ = Process.Start(startInfo);
    }
}
