# Downora

Downora is a native Windows desktop app for downloading video and audio from YouTube, SoundCloud, Vimeo and the other sites [yt-dlp](https://github.com/yt-dlp/yt-dlp) supports. It includes a download manager, an audio converter and a local media library.

Built with C#, .NET 10 and WPF (MVVM). Media extraction runs through yt-dlp and Deno, conversion through FFmpeg, and data is stored in SQLite via Entity Framework Core.

> Downora is for content you own or are authorized to download. It does not remove DRM, bypass paywalls or get around access controls.

## Status

**Phases 1–5 of 11 are complete.**

- **Phase 1, foundation:** the solution, the Generic Host with DI, Serilog logging, SQLite with migrations, persistent settings, crash recovery and CI.
- **Phase 2, shell UI:** the Fluent Velocity design system in dark and light, a sidebar with all seven pages, English and Arabic with a right-to-left layout, and real data on every page.
- **Phase 3, analyze:** paste a YouTube, SoundCloud, Vimeo or other supported link and Downora validates it, detects the platform, and shows the title, uploader, duration, thumbnail and every available quality with its estimated size (or the full list of a playlist). Clear messages for sign-in-only, region-locked, DRM-protected and unavailable content. yt-dlp, FFmpeg and Deno are copied from the bundle or downloaded from their official releases with SHA-256 verification, and can be installed or updated from Settings › Tools.

- **Phase 4, download:** pick a quality (or audio only: MP3, M4A or Opus) on the Downloader screen and the file really downloads into your download folder, with live progress (percent, size, speed, time left, merging/finishing stages). Each download is a saved job (waiting → extracting → downloading → processing → completed / failed / cancelled), so the Download Queue, History, Home statistics and crash recovery all show real data, and Cancel kills yt-dlp and removes the partial files. Free disk space is checked first, file names are made Windows-safe, and a new download never overwrites an older file. Optional, off by default: use the sign-in of your own browser (Edge, Chrome, Firefox or Brave) for content your account can already see, from Settings › Browser sign-in.

- **Phase 5, download manager:** downloads now run through a real queue. At most the chosen number run at once (Settings › Downloads, 1 to 10; the rest wait in the order they were added), and changing the number takes effect immediately. Pause a download (or all of them) and what is downloaded so far is kept; resume it later and it continues where it stopped. Cancel works on running, waiting and paused downloads. A failed download has a Try again button, on the queue card and in History, that continues from its partial files; a cancelled one starts again from the beginning. Turn off "Start downloads automatically" to hold new downloads until you start them. Closing Downora pauses what was running and keeps its files.

See [docs/roadmap.md](docs/roadmap.md).

## Requirements

- Windows 10 (version 2004) or Windows 11, x64
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (`global.json` pins 10.0.100 or a later feature band)
- Visual Studio 2026 with the **.NET desktop development** workload, or JetBrains Rider

## Build and run

```powershell
git clone https://github.com/Andrewaziz99/Downora.git
cd Downora
dotnet build Downora.sln
dotnet run --project src/Downora.App
dotnet test --solution Downora.sln
```

In Visual Studio, open `Downora.sln`, set **Downora.App** as the startup project and press F5.

The first launch creates:

| Path | Contents |
| --- | --- |
| `%LocalAppData%\Downora\downora.db` | Queue, history, library and settings (SQLite, WAL mode) |
| `%LocalAppData%\Downora\logs\` | Daily rolling logs, kept for 14 days |
| `%LocalAppData%\Downora\tools\` | yt-dlp, FFmpeg/FFprobe and Deno (from Phase 3) |
| `%UserProfile%\Downloads\Downora\` | Default download folder |

## Solution layout

```text
src/
  Downora.Core/            Models, enums, interfaces, pure business rules (no UI, no IO)
  Downora.Infrastructure/  EF Core database, settings, queries, logging, processes, tools
  Downora.Presentation/    View models, navigation, localization (en/ar) - no WPF, testable anywhere
  Downora.App/             WPF views, theme, Windows services (composition root)
tests/
  Downora.Core.Tests/            Unit tests
  Downora.Infrastructure.Tests/  Real SQLite files in temp folders
  Downora.Presentation.Tests/    View models, navigation and translations
design/                    Stitch design reference and logo sources
tools/                     strings.py (UI text), check_xaml_resources.py (CI check)
docs/                      Developer guide and roadmap
installer/                 Inno Setup script (Phase 10)
```

Dependencies point one way: App → Presentation / Infrastructure → Core. See [docs/developer-guide.md](docs/developer-guide.md) for the conventions.

## Third-party tools

Downora drives these as separate processes and will bundle them in the installer:

| Tool | License |
| --- | --- |
| [yt-dlp](https://github.com/yt-dlp/yt-dlp) | Unlicense |
| [FFmpeg](https://ffmpeg.org/) (LGPL build) | LGPL 2.1+ |
| [Deno](https://github.com/denoland/deno) | MIT |
