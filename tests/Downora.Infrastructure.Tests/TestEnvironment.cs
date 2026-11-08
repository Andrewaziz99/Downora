using Downora.Core.Interfaces;
using Downora.Infrastructure.Database;
using Downora.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Downora.Infrastructure.Tests;

/// <summary>
/// A real SQLite file in a throw-away folder, wired with the same DI registration the app uses.
/// Create a second environment on the same folder to simulate an app restart.
/// </summary>
internal sealed class TestEnvironment : IAsyncDisposable
{
    private readonly bool _ownsFolder;
    private readonly ServiceProvider _provider;

    private TestEnvironment(string root, bool ownsFolder)
    {
        Root = root;
        _ownsFolder = ownsFolder;
        Paths = new AppPaths(Path.Combine(root, "data"), Path.Combine(root, "downloads"));

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDownoraInfrastructure(Paths);
        _provider = services.BuildServiceProvider();
    }

    public string Root { get; }
    public IAppPaths Paths { get; }

    public T Get<T>() where T : notnull => _provider.GetRequiredService<T>();

    public Task<DownoraDbContext> CreateDbAsync() =>
        _provider.GetRequiredService<IDbContextFactory<DownoraDbContext>>().CreateDbContextAsync();

    public static Task<TestEnvironment> StartAsync() =>
        StartAsync(Path.Combine(Path.GetTempPath(), "downora-tests", Guid.NewGuid().ToString("N")), ownsFolder: true);

    private static async Task<TestEnvironment> StartAsync(string root, bool ownsFolder)
    {
        var env = new TestEnvironment(root, ownsFolder);
        await env.Get<IDatabaseInitializer>().InitializeAsync();
        return env;
    }

    /// <summary>Disposes this environment and starts a new one on the same data, like closing and reopening the app.</summary>
    public async Task<TestEnvironment> RestartAsync()
    {
        await _provider.DisposeAsync();
        SqliteConnection.ClearAllPools();
        return await StartAsync(Root, _ownsFolder);
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        SqliteConnection.ClearAllPools();
        if (_ownsFolder && Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}
