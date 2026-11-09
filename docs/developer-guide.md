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
