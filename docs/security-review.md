# Security review (Phase 9)

Downora is a local, single-user Windows app. It runs three programs (yt-dlp, FFmpeg/FFprobe and Deno) on links and files that come from the internet, so the review followed the data: what an attacker could control, and what Downora does with it.

## What is untrusted

| Input | Where it comes from |
| --- | --- |
| A pasted link | The user, possibly from a hostile page or message |
| Metadata: titles, uploaders, thumbnail and playlist URLs, JSON shapes | The website, relayed by yt-dlp |
| yt-dlp and FFmpeg output lines | The tools, which parse hostile websites |
| A downloaded file | The website |
| Tool downloads (yt-dlp, FFmpeg, Deno) | GitHub releases |

Out of scope: an attacker who can already run code as the same Windows user or edit the Downora database. They could do anything the user can, with or without Downora.

## Controls that were already in place

- **No shell, ever.** Every tool is started with `ProcessStartInfo.ArgumentList`; the link comes after `--`, so it can never be read as an option. Tests cover hostile links (`--exec calc`, spaces, newlines, `file:`, `ftp:`, `javascript:`).
- **Only web links.** `UrlValidator` accepts absolute http(s) links, caps the length at 2048 and rejects whitespace and control characters.
- **File names from titles are sanitized** (`FileNames`): invalid and bidirectional characters removed, reserved device names (CON, NUL, COM1…) renamed, trailing dots dropped, length capped, a title such as `..\..\Windows\evil` flattened into one harmless name.
- **Tools are verified.** Each install downloads over HTTPS from the official release, checks the published SHA-256, and takes only the named files out of a zip (entry names are matched, never used as paths, so a zip cannot write outside the tools folder).
- **Browser cookies are opt-in** and never read, stored or logged by Downora; only the browser name is passed to yt-dlp.
- **Partial files** are confined to `<download folder>\.downora-partial\<job id>`; cancelling deletes exactly that folder.
- **Database access is EF Core only**, and search patterns escape `%` and `_`.
- **Thumbnails** load only from http(s) addresses or existing local files.

## Findings and fixes

| # | Severity | Finding | Fix | Test |
| --- | --- | --- | --- | --- |
| 1 | Medium | The Library's "Open" handed any file to Windows with `UseShellExecute`. A downloaded `.exe`, `.bat`, `.lnk` or script would have run. | Only known audio and video types are opened (`MediaFileTypes`); anything else is shown in Explorer instead. | `MediaFileTypesTests` |
| 2 | Low | Explorer was started as the bare name `explorer.exe`, which Windows may resolve from the app's own folder first. | The full path under the Windows folder is used. | (App project; reviewed) |
| 3 | Low | The log wrote full links, which can carry sign-in tokens or signatures. | `LogSafe.Url` drops the user name, password and fragment and blanks the value of token-like query parameters. | `LogSafeTests` |
| 4 | Low | A link with a user name and password (`https://user:pass@host/…`) was accepted, then saved in the database and passed on in the clear. | Rejected with a clear message; Downora never stores credentials. | `HostileInputTests` |
| 5 | Low | yt-dlp loads plugin code from user-writable folders. `--ignore-config` does not stop that. | `--no-plugin-dirs` on every yt-dlp call (analysis and download). | `HardenedToolCallTests` |
| 6 | Low | The "use my own FFmpeg" setting accepted any full path, so it could point at a script. | Must be a full path ending in `.exe`. | `SettingsHardeningTests` |
| 7 | Low | A download's whole progress output was kept in memory for the lifetime of the process. | Callers that follow output line by line no longer buffer it (`CaptureStandardOutput`). | `HardenedToolCallTests` |
| 8 | Medium | A title containing half an emoji (an unpaired surrogate, which valid JSON can carry) made `string.Normalize` throw, failing the download with an unreadable error. Found by the new fuzz test. | Broken halves are replaced before normalizing. | `FileNameRobustnessTests` |
| 9 | Medium | The yt-dlp JSON parser threw `OverflowException` on an absurd duration and `InvalidOperationException` on non-object entries in `formats`. Both would have surfaced as a generic error instead of a normal "couldn't read" one. Found by the new fuzz test. | Every read first checks the element's type, numbers must be finite and in range, durations are capped at ten years. | `MalformedInputTests` |

## Accepted risks and notes

- **Checksums come from the same place as the files** (GitHub releases). They catch corruption and a swapped file, not a compromised release. Pinning hashes would stop automatic updates; the trade-off is accepted.
- **yt-dlp fetches any web address the user pastes,** including addresses on the local network. That is the user's own request from their own machine.
- **Titles are shown as sent by the site,** including right-to-left marks. File names are cleaned; on-screen titles are not.
- **FFmpeg and Deno process hostile data.** Keeping them current is the defence; yt-dlp updates itself daily, FFmpeg and Deno are refreshed by reinstalling.
- **Logs contain titles and file paths** (not full links or cookies) in `%LocalAppData%\Downora\logs`, 14 days, 20 MB per file.
- **One Downora per user.** The single-instance event can be set by any local process; the only effect is that the window is shown.

## Dependencies

NuGet auditing is on for direct and transitive packages (`NuGetAuditMode=all`, level low) and warnings are errors, so a package with a known vulnerability fails the build. `dotnet list package --vulnerable --include-transitive` and `--deprecated` both report nothing at this point.

## Reporting a problem

See [SECURITY.md](../SECURITY.md).
