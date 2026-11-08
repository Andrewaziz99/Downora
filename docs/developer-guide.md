# Developer guide

## Architecture rules

- **Core** (`net10.0`) holds models, enums, interfaces and pure logic. It never references WPF, EF Core, `System.Diagnostics.Process` or the file system beyond path helpers, so every rule in it is unit-testable on any OS.
- **Infrastructure** (`net10.0`) implements Core interfaces: the database, settings, logging, and (from Phase 3) the yt-dlp/FFmpeg processes. Register new services in `DependencyInjection.AddDownoraInfrastructure`.
- **App** (`net10.0-windows`, WPF) holds views, view models and Windows-only services such as Explorer and the clipboard. `App.xaml.cs` is the composition root.
- Views contain no logic. View models use CommunityToolkit.Mvvm (`[ObservableProperty]`, `[RelayCommand]`) and depend only on interfaces.
- Data access goes through `IDbContextFactory<DownoraDbContext>`: create a context per operation and dispose it. Don't hold a context in a singleton.
- Never block the UI thread. Await IO, and in Infrastructure use `ConfigureAwait(false)`.
- Pass process arguments with `ProcessStartInfo.ArgumentList`, never through a shell or a joined string.

## Build settings

`Directory.Build.props` turns on nullable reference types, `latest-recommended` analyzers and **warnings as errors**. Fix a warning; don't suppress it, unless the reason belongs in a comment next to a `#pragma`.

Package versions live in `Directory.Packages.props` (central package management). Add a `PackageVersion` there and a version-less `PackageReference` in the project.

`EnableWindowsTargeting` lets the WPF project compile on Linux or macOS. It still only runs on Windows.

## Database migrations

Enums are stored by name, `TimeSpan` as ticks and `DateTime` as UTC. After changing a model or a configuration in `Database/Configurations.cs`:

```powershell
dotnet tool restore
dotnet ef migrations add <Name> --project src/Downora.Infrastructure --startup-project src/Downora.Infrastructure --output-dir Database/Migrations
```

The app applies pending migrations at startup (`DatabaseInitializer`). CI fails if the model has changes with no migration.

## Tests

```powershell
dotnet test --solution Downora.sln
```

Tests run on Microsoft.Testing.Platform with xUnit v3 (`global.json` opts in). Infrastructure tests use a real SQLite file in a temp folder through `TestEnvironment`, which wires the same DI registration as the app. `RestartAsync()` simulates closing and reopening Downora.

From Phase 3, external processes are reached only through `IProcessRunner`, so unit tests replay recorded yt-dlp/FFmpeg output instead of running the tools.

## Logs

Serilog writes to `%LocalAppData%\Downora\logs\downora-YYYYMMDD.log`. Use `[LoggerMessage]` source-generated methods for log calls (CA1848). Raw tool output belongs in the log, not in messages shown to the user.
