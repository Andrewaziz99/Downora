# Downora

Downora is a native Windows desktop app for downloading video and audio from YouTube, SoundCloud, Vimeo and the other sites [yt-dlp](https://github.com/yt-dlp/yt-dlp) supports. It includes a download manager, an audio converter and a local media library.

Built with C#, .NET 10 and WPF (MVVM). Media extraction runs through yt-dlp and Deno, conversion through FFmpeg, and data is stored in SQLite via Entity Framework Core.

> Downora is for content you own or are authorized to download. It does not remove DRM, bypass paywalls or get around access controls.

## Status

**Phases 1–9 of 11 are complete.**

- **Phase 1, foundation:** the solution, the Generic Host with DI, Serilog logging, SQLite with migrations, persistent settings, crash recovery and CI.
- **Phase 2, shell UI:** the Fluent Velocity design system in dark and light, a sidebar with all seven pages, English and Arabic with a right-to-left layout, and real data on every page.
- **Phase 3, analyze:** paste a YouTube, SoundCloud, Vimeo or other supported link and Downora validates it, detects the platform, and shows the title, uploader, duration, thumbnail and every available quality with its estimated size (or the full list of a playlist). Clear messages for sign-in-only, region-locked, DRM-protected and unavailable content. yt-dlp, FFmpeg and Deno are copied from the bundle or downloaded from their official releases with SHA-256 verification, and can be installed or updated from Settings › Tools.

- **Phase 4, download:** pick a quality (or audio only: MP3, M4A or Opus) on the Downloader screen and the file really downloads into your download folder, with live progress (percent, size, speed, time left, merging/finishing stages). Each download is a saved job (waiting → extracting → downloading → processing → completed / failed / cancelled), so the Download Queue, History, Home statistics and crash recovery all show real data, and Cancel kills yt-dlp and removes the partial files. Free disk space is checked first, file names are made Windows-safe, and a new download never overwrites an older file. Optional, off by default: use the sign-in of your own browser (Edge, Chrome, Firefox or Brave) for content your account can already see, from Settings › Browser sign-in.

- **Phase 5, download manager:** downloads now run through a real queue. At most the chosen number run at once (Settings › Downloads, 1 to 10; the rest wait in the order they were added), and changing the number takes effect immediately. Pause a download (or all of them) and what is downloaded so far is kept; resume it later and it continues where it stopped. Cancel works on running, waiting and paused downloads. A failed download has a Try again button, on the queue card and in History, that continues from its partial files; a cancelled one starts again from the beginning. Turn off "Start downloads automatically" to hold new downloads until you start them. Closing Downora pauses what was running and keeps its files.
- **Phase 6, FFmpeg:** audio can now be saved as MP3, M4A, Opus, FLAC (lossless) or WAV. Downloads carry their title, uploader and date in the file's own tags and the thumbnail as cover art (turn either off in Settings › Downloads; WAV and WebM can't hold a cover picture). After every download, FFprobe opens the finished file and checks it has the audio or video you asked for and isn't cut short; a file that fails is removed and shown as "damaged" with a Try again button, instead of being reported as done.
- **Phase 7, playlists and library:** paste a playlist link and every item is listed with a tick box (private or deleted items can't be ticked). Pick video (with a quality) or audio (with a format), press "Download selected", and the items join the queue in playlist order; items already in your queue are skipped, and the page shows "3 of 12 finished" as they complete. The Media Library now fills itself: each finished download is added with its size, length, resolution and codecs read by FFprobe, downloads from earlier versions are added the first time you open the app, files you moved or deleted are marked "File missing", and "Remove from library" drops an entry without touching the file.
- **Phase 8, settings, history, tools, tray, startup:** Settings gains "Keep download history" (forever, 30, 90, 180 days or a year; older entries are removed, files and library untouched), "Keep yt-dlp up to date" (checked when Downora starts, at most once a day and never while downloads run; on by default), "Use my own FFmpeg", and a Windows section: start Downora when you sign in, start in the tray, and keep running in the tray when you close the window. There is now a tray icon (Open, Pause all, Resume all, Exit; its tooltip shows how many downloads are running), and only one Downora can run at a time: starting a second one brings the first to the front. History can be filtered by Completed, Failed or Cancelled, and any entry can be removed on its own.
- **Phase 9, review:** a security review ([docs/security-review.md](docs/security-review.md), [SECURITY.md](SECURITY.md)) and a performance review ([docs/performance-review.md](docs/performance-review.md)). Fixes include: the Library only opens known audio and video types (a downloaded program or script is shown in Explorer, never run); links with a user name or password are refused; logs no longer contain tokens from links; yt-dlp no longer loads plugin code; very long playlists and queues are virtualized and the Library shows 120 entries at a time with "Show more". Random-input tests found and fixed two real bugs (half an emoji in a title, and oddly shaped yt-dlp output). Line coverage is 94% and CI now fails if it drops.

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
