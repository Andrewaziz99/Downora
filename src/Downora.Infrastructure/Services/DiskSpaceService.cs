using Downora.Core.Interfaces;
using Downora.Core.Models;

namespace Downora.Infrastructure.Services;

internal sealed class DiskSpaceService : IDiskSpaceService
{
    public DiskUsage? GetUsage(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
        {
            return null;
        }

        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(path));
            if (string.IsNullOrEmpty(root))
            {
                return null;
            }

            var drive = new DriveInfo(root);
            return drive.IsReady
                ? new DiskUsage(drive.Name.TrimEnd('\\', '/') is { Length: > 0 } name ? name : drive.Name, drive.TotalSize, drive.AvailableFreeSpace)
                : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }
}
