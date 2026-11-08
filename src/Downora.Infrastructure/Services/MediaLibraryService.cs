using Downora.Core.Interfaces;
using Downora.Core.Models;
using Downora.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Downora.Infrastructure.Services;

internal sealed class MediaLibraryService(IDbContextFactory<DownoraDbContext> contextFactory) : IMediaLibraryService
{
    public async Task<IReadOnlyList<MediaItem>> QueryAsync(LibraryQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var items = db.MediaItems.AsNoTracking();

        if (query.Kind is { } kind)
        {
            items = items.Where(m => m.MediaKind == kind);
        }

        if (SearchPattern.FromText(query.Search) is { } pattern)
        {
            items = items.Where(m =>
                EF.Functions.Like(m.Title ?? string.Empty, pattern, SearchPattern.Escape) ||
                EF.Functions.Like(m.Artist ?? string.Empty, pattern, SearchPattern.Escape) ||
                EF.Functions.Like(m.FilePath, pattern, SearchPattern.Escape));
        }

        items = (query.Sort, query.Descending) switch
        {
            (LibrarySort.Title, false) => items.OrderBy(m => m.Title ?? m.FilePath),
            (LibrarySort.Title, true) => items.OrderByDescending(m => m.Title ?? m.FilePath),
            (LibrarySort.Duration, false) => items.OrderBy(m => m.Duration),
            (LibrarySort.Duration, true) => items.OrderByDescending(m => m.Duration),
            (LibrarySort.FileSize, false) => items.OrderBy(m => m.FileSizeBytes),
            (LibrarySort.FileSize, true) => items.OrderByDescending(m => m.FileSizeBytes),
            (_, false) => items.OrderBy(m => m.AddedAtUtc),
            _ => items.OrderByDescending(m => m.AddedAtUtc),
        };

        return await items.Take(Math.Max(1, query.Take)).ToListAsync(cancellationToken).ConfigureAwait(false);
    }
}
