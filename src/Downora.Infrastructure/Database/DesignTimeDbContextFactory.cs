using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Downora.Infrastructure.Database;

/// <summary>Used only by <c>dotnet ef</c> to create migrations; the app builds its context through DI.</summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<DownoraDbContext>
{
    public DownoraDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<DownoraDbContext>()
            .UseSqlite("Data Source=design-time.db")
            .Options;
        return new DownoraDbContext(options);
    }
}
