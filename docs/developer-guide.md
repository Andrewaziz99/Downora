# Developer guide

## Architecture rules

- **Core** (`net10.0`) holds models, enums, interfaces and pure logic. It never references WPF, EF Core, `System.Diagnostics.Process` or the file system beyond path helpers, so every rule in it is unit-testable on any OS.
- **Infrastructure** (`net10.0`) implements Core interfaces: the database, settings, logging, and (from Phase 3) the yt-dlp/FFmpeg processes. Register new services in `DependencyInjection.AddDownoraInfrastructure`.
- **Presentation** (`net10.0`) holds view models, navigation and localization. It has no WPF reference, so view models are unit-tested on any OS. UI-only needs (theme, dialogs, Explorer) are interfaces in `Presentation/Services`, implemented by App.
- **App** (`net10.0-windows`, WPF) holds views, the theme resources and Windows-only services. `App.xaml.cs` is the composition root.
- Views contain no logic. View models use CommunityToolkit.Mvvm (`[ObservableProperty]`, `[RelayCommand]`) and depend only on interfaces.
- Pages derive from `PageViewModel`, reload in `OnNavigatedToAsync`, and wrap their work in `RunSafeAsync` so a failure shows a message instead of crashing the window. Register a new page in `AddDownoraPresentation` and map it to its view with a `DataTemplate` in `App.xaml`.
- Don't add a button or setting before its feature works. Pages without data show an empty state.
- Data access goes through `IDbContextFactory<DownoraDbContext>`: create a context per operation and dispose it. Don't hold a context in a singleton.
- Never block the UI thread. Await IO, and in Infrastructure use `ConfigureAwait(false)`.
- Pass process arguments with `ProcessStartInfo.ArgumentList`, never through a shell or a joined string.

## UI text and languages

All UI text lives in `tools/strings.py` as English/Arabic pairs. Edit it there, then regenerate both resource files:

```powershell
python tools/strings.py
```

In XAML, use `Text="{l:Loc Key}"`, which updates live when the language changes. In view models, use `L["Key"]` or `L.Format("Key", args)`. A test fails if the two languages have different keys, a blank value, or different `{0}` placeholders. Arabic turns on `FlowDirection.RightToLeft` for the whole window: lay views out with leading/trailing margins and grids, not absolute left/right positions.

## Theme

`Resources/Theme/` follows the Fluent Velocity design system (`design/stitch/fluent_velocity/DESIGN.md`):

- `Palette.Dark.xaml` / `Palette.Light.xaml` hold the theme colors, and `ThemeService` swaps them. Reference them with `DynamicResource`.
- `Tokens.xaml` holds brand, status, type and spacing tokens, the same in both themes.
- `Styles.xaml` and `Templates.xaml` hold the component styles and shared item templates.

A `StaticResource` key that doesn't exist only fails at runtime, so CI runs `python tools/check_xaml_resources.py`. Run it after editing XAML.

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

## Downloading (Phase 4)

- `IDownloadEngine` (Core) downloads one item; `YtDlpDownloadEngine` (Infrastructure) is the only code that knows yt-dlp's command line. `YtDlpDownloadArguments` builds the arguments (pure, unit-tested), `YtDlpProgressParser` and `ProgressTracker` turn its machine-readable output into progress (pure, tested against `tests/.../Fixtures/dl_*.out`).
- yt-dlp prints download progress on stdout but post-processing steps on stderr, so the engine reads both. Its console encoding garbles non-Latin text in the printed file path (Arabic titles), so the engine also finds the finished file by the unique name it chose. Never rely on yt-dlp's printed text for names.
- Every job keeps its partial files in `<download folder>\.downora-partial\<job id>`. Cancelling the token only stops the tool; the job service calls `DiscardPartialFilesAsync` when the *user* cancels, and not when the app closes or the tool crashes, so a later resume (Phase 5) can continue.
- `IDownloadJobService` (`DownloadJobService`) creates the job row, runs the engine, applies the state rules in `DownloadJobTransitions` (Core, pure), saves changes (progress at most every 2 s) and raises `JobUpdated` from a background thread. A Phase 5 scheduler replaces only the "start immediately" step in `StartAsync`.
- Screens never subscribe to the service directly: `DownloadFeed` (Presentation) moves updates to the UI thread through `IUiThread` and flags real state changes. Item cards (`DownloadItemViewModel`) update in place through `Apply`.
- Browser sign-in is opt-in (`AppSettings.UseBrowserSession`, `BrowserForCookies`). `CookieBrowsers` is the only list of accepted browsers; the name is passed to `--cookies-from-browser` and nothing else. Never read, copy, store or log cookie contents.
- Live tests (real yt-dlp and FFmpeg, public 19-second video) run with `DOWNORA_LIVE_TESTS=1`; they look for the tools in `%LocalAppData%\Downora\tools` or `DOWNORA_TOOLS`.

## The download queue (Phase 5)

- `DownloadJobService` is the scheduler. `PumpAsync` is the only place a run starts: it fills free slots (`MaxConcurrentDownloads`, read live) with the oldest `Waiting` jobs, and it is called whenever a job is added, resumed or retried, a run ends, or the setting changes. Never start a run anywhere else.
- States: new jobs are `Waiting` (or `Paused` when "start automatically" is off). Pause keeps partial files (the run is stopped with `PauseRequested`, so it ends `Paused` instead of `Cancelled`); only cancel discards them. Resume moves `Paused` to `Waiting`; Retry moves `Failed` or `Cancelled` to `Waiting` (a failed job continues from its files, a cancelled one restarts). Pausing during FFmpeg's joining step is refused. The rules are in `DownloadJobTransitions` (Core, pure).
- Queue order is `QueueOrder`, strictly increasing per job added (`NextQueueOrder`), so jobs added in the same clock tick keep their order and a resumed or retried job keeps its original place.
- The host starts the service before the database and settings are ready, so `IHostedService.StartAsync` only subscribes to settings changes. `App.xaml.cs` calls `IDownloadJobService.StartQueueAsync()` after startup to run jobs left waiting. Do not touch settings or the database from `StartAsync`.
- Screens call `IDownloadItemActions` (pause, resume, retry, cancel) and never the service directly; actions swallow and log failures so a click can't crash the window.
- Queue tests use `FakeEngine` with one gate per download (`DownloadQueueTests`), so concurrency is tested deterministically; wait for background work with `UntilAsync`, never a fixed sleep.

## Tags, cover art and file verification (Phase 6)

- yt-dlp does the conversion and tagging (`-x --audio-format`, `--embed-metadata`, `--convert-thumbnails jpg --embed-thumbnail`) with the FFmpeg Downora manages. Cover art is skipped for WAV and WebM (`YtDlpDownloadArguments.CanHoldCoverArt`); asking for it there would only print a warning. `EmbedMetadata` and `EmbedCoverArt` are settings read when a job starts, so changing them affects the next run.
- After yt-dlp exits successfully the engine reports the `Verifying` stage and calls `IMediaFileVerifier` (`FfprobeFileVerifier`: FFprobe JSON, then the pure `FileVerificationRules`). A file with no audio (audio jobs) or no video (video jobs), no length, or more than 10% (at least 5 s) shorter than the analysed length fails as `CorruptFile` and is deleted. An embedded cover shows up as a video stream, so audio files are never required to be video-free.
- The check never blocks on the tool: no FFprobe installed or a probe timeout means "not checked" and the file is kept.
- Adding a column to `AppSettings` needs a migration: `dotnet ef migrations add <Name> --project src/Downora.Infrastructure` (the Infrastructure project is its own startup project via `DesignTimeDbContextFactory`). Give new `bool` columns the default you want existing users to get, since EF writes `false`.

## Playlists and the library (Phase 7)

- Analysis lists playlist entries without opening each one (`--flat-playlist`). `IDownloadJobService.StartPlaylistAsync` turns the ticked entries into one `PlaylistBatch` and one job per item, all saved in one go, with strictly increasing `QueueOrder` so the queue runs them in playlist order. Items are not analysed individually: a video job downloads with the chosen quality preset (`-S res:N`), so there is no size estimate for them. Duplicates (already queued in the same format, repeated in the playlist) and non-web links are skipped and counted in `PlaylistStartResult`.
- The Downloader follows the batch through `DownloadFeed` by job id (`_batch`), so "x of N finished" is live without the screen polling.
- `MediaLibraryService` owns the library. `LibraryIndexer` (a hosted service that, like the queue, only subscribes in `StartAsync`) calls `AddCompletedAsync` when a job completes. `SyncAsync` is the only maintenance entry point: it adds completed jobs that have no entry (older downloads) and refreshes `IsMissing`. It never throws; the app calls it once after startup without waiting, and the Library screen calls it each time it is shown.
- One entry per file path (unique index): downloading over the same path updates the entry. Entry details come from `IMediaProbe` (FFprobe). An embedded cover is a "video" stream with `attached_pic=1`; it is ignored for resolution and codec, and audio entries never get a video size.
- `MediaItem.ThumbnailPath` holds a local file or, for downloads, the thumbnail's web address (the thumbnail converter loads either). Removing an entry never deletes the file or the download history.

## Settings, maintenance, tray and startup (Phase 8)

- `AppMaintenance` (Infrastructure) runs after startup (`IAppMaintenance.RunStartupTasksAsync`, called from `App.xaml.cs` and not awaited) and, for history retention, whenever the setting changes. It never throws. The yt-dlp update check runs at most every 24 hours (`LastToolCheckUtc`, recorded even when the check fails, so a dead network doesn't retry on every start) and is skipped while any download is running (nothing is recorded then, so the next start tries again). Pruning deletes only finished jobs; their library entries are unlinked, never deleted.
- `StartupSynchronizer` keeps the HKCU Run entry in line with `LaunchAtStartup` (`IStartupRegistry` is the Windows-only part, implemented in the App). The command is `"<exe>" --startup`; only that argument plus `StartMinimized` keeps the window hidden, so launching Downora by hand always shows it. It also corrects an entry that points at an old location at every start.
- The tray (`TrayService`, WinForms `NotifyIcon`, which is why the App project has `UseWindowsForms`) and `WindowLifecycle` decide together what closing means: with "close to tray" the main window's `Closing` is cancelled and the window hidden; the tray's Exit and Windows sign-out (`SessionEnding`) call `MarkExiting` first so they really close. The icon is disposed in `OnExit`, or it lingers in the tray.
- `SingleInstance` (named mutex plus a named event) makes a second launch ask the first to show its window and exit. For development, set `DOWNORA_ALLOW_MULTIPLE_INSTANCES=1` to run several copies (they share one database, so use it only for short checks).
- Adding a setting column still needs a migration with the right default for existing users (`CloseToTray` and `LastToolCheckUtc` default to off / null).
