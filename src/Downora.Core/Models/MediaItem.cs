using Downora.Core.Enums;

namespace Downora.Core.Models;

/// <summary>A file in the local media library. Kept independently of history, so clearing history never hides files.</summary>
public class MediaItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? DownloadJobId { get; set; }

    public required string FilePath { get; set; }
    public MediaKind MediaKind { get; set; }

    public string? Title { get; set; }
    public string? Artist { get; set; }
    public string? Album { get; set; }
    public TimeSpan? Duration { get; set; }
    public long FileSizeBytes { get; set; }

    public int? Width { get; set; }
    public int? Height { get; set; }
    public string? VideoCodec { get; set; }
    public string? AudioCodec { get; set; }
    public int? BitrateKbps { get; set; }

    public string? ThumbnailPath { get; set; }
    public string? SourceUrl { get; set; }
    public SourcePlatform Platform { get; set; }

    public DateTime AddedAtUtc { get; set; }

    /// <summary>Set when the file was moved or deleted outside the app.</summary>
    public bool IsMissing { get; set; }
}
