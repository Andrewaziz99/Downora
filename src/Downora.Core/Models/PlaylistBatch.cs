namespace Downora.Core.Models;

/// <summary>A playlist or collection the user downloaded items from; jobs point to it for aggregate progress.</summary>
public class PlaylistBatch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string SourceUrl { get; set; }
    public string? Title { get; set; }
    public string? Uploader { get; set; }
    public int ItemCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public List<DownloadJob> Jobs { get; } = [];
}
