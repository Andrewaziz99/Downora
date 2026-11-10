# Downora user guide

Downora is a Windows app for downloading video and audio from YouTube, SoundCloud, Vimeo and many other sites, and keeping what you download organised.

**Only download content you own or are allowed to access.** Downora does not remove DRM or get around paywalls and other access controls, and it never will.

العربية: [دليل المستخدم](user-guide.ar.md)

## Installing

1. Download `Downora-Setup-<version>.exe` from the [Releases page](https://github.com/Andrewaziz99/Downora/releases) and, if you like, check it against the `.sha256` file next to it.
2. Run it. It installs for you alone (no administrator password) and offers a desktop shortcut. English and Arabic are both available.
3. Everything Downora needs is inside, including the programs that do the downloading (yt-dlp, FFmpeg and Deno), so it works straight away, even without a connection until you download something.

If Windows says "Windows protected your PC" (SmartScreen), the installer has not been signed yet. Choose **More info**, then **Run anyway**, if you downloaded it from the Releases page above.

To update, run a newer installer: it replaces the old version and keeps your settings and history.

## Downloading one link

1. Open **Downloader** (or paste a link on **Home**).
2. Paste a link and press **Analyze**. Downora shows the title, uploader, length and what the site offers.
3. For a video, pick a quality (Best, 1080p, 720p… the list shows what is really available, with an estimated size) and press **Download**. For audio only, pick **MP3**, **M4A**, **Opus**, **FLAC** (lossless) or **WAV** and press **Download audio**.
4. The download appears in **Download Queue** with live progress. When it finishes it is in your download folder (**Settings** shows where) and in **Media Library**.

Downloaded files carry their title and uploader inside, and the thumbnail as cover art, so music players show them properly. (WAV and WebM cannot hold a cover picture.) After each download Downora opens the file to check it plays and wasn't cut short; a damaged file is removed and shown as failed, with a **Try again** button.

## Playlists

Paste a playlist link. Every item is listed with a tick box; private or deleted items can't be ticked. Choose **Video** (with a quality) or **Audio** (with a format), then **Download selected**. The items join the queue in playlist order and the page shows "3 of 12 finished" as they complete. Items already in your queue are skipped.

## The queue

**Download Queue** shows what is running, waiting and paused.

- **Pause** a download (or **Pause all**): what was downloaded is kept, and **Resume** continues from there.
- **Cancel** stops a download and deletes its partial files.
- **Try again** on a failed download continues where it stopped; on a cancelled one it starts over.
- Several downloads run at once (3 by default; change it in Settings, 1–10). The rest wait in the order you added them.
- Closing Downora pauses what was running and keeps its files; it picks up when you open it again.

## Media Library and History

**Media Library** lists your finished downloads with their size, length, resolution and format. Search by title or artist, filter by video or audio, sort, **Open** a file, **Show in folder**, or remove an entry from the list (the file stays on your disk). A file you moved or deleted is marked "File missing". Only known audio and video files are opened; anything else is shown in its folder.

**History** lists every finished, failed and cancelled download. Filter by status, search, **Try again** on a failed one, or remove an entry. Clearing History never deletes files or your library.

## Settings

- **Appearance:** Dark, Light or follow Windows; English or Arabic (Arabic switches the whole app to right-to-left).
- **Downloads:** download folder, how many downloads at once, start automatically (turn off to hold new downloads until you start them), add title and artist to files, add cover art, and how long to keep history.
- **Windows:** start Downora when you sign in, start in the tray, and keep running in the tray when you close the window.
- **Browser sign-in:** see below.
- **Tools:** the versions of yt-dlp, FFmpeg and Deno; **Check for updates** for yt-dlp; **Keep yt-dlp up to date** (checked when Downora starts, at most once a day, never while downloads run); and **Use my own FFmpeg** if you prefer your own `ffmpeg.exe`.

## The tray icon

Downora puts an icon in the notification area. Click it to open the window; right-click for **Open Downora**, **Pause all downloads**, **Resume all downloads** and **Exit**. Its tooltip shows how many downloads are running. If you turn on "keep running in the tray", closing the window leaves downloads going and **Exit** really quits. Only one Downora runs at a time; opening it again just brings the first one forward.

## Videos that need you to be signed in

Some videos (members-only, age-restricted, private to your account) can only be seen by a signed-in person. If you are allowed to see one, turn on **Settings > Browser sign-in** and choose your browser. Downora then lets yt-dlp use the sign-in your browser already has. It is **off unless you turn it on**; Downora never copies, stores or logs your cookies, and it only reads them from your own browser each time it runs. Some browsers (Chrome, Edge) lock their data while open, so close the browser first; some (Brave) may refuse altogether, in which case choose another browser or turn the option off.

## When something goes wrong

| You see | What it means | What to do |
| --- | --- | --- |
| Sign-in required | The site wants a signed-in user | Turn on Browser sign-in (above), if you are allowed to see it |
| Protected content | The video uses DRM | Downora can't and won't download it |
| Not available in your region | The site blocks your country | Nothing Downora can do |
| Content unavailable | Deleted or private | Check the link in your browser |
| Couldn't reach the site | Network problem | Try again; if it keeps failing, check for updates under Tools (sites change often) |
| Couldn't read your browser sign-in | The browser locked its data or refuses access | Close the browser, pick another, or turn the option off |
| The downloaded file is damaged | The file failed Downora's check and was removed | Try again |
| Not enough space | The drive is full | Free some space or choose another folder in Settings |
| A tool is missing | yt-dlp, FFmpeg or Deno isn't installed | Press **Install** in Settings > Tools |

If a site that used to work stops working, update yt-dlp: **Settings > Tools > Check for updates**.

## Your data and privacy

Downora keeps its database (settings, history, library), logs and a copy of the tools in `%LocalAppData%\Downora`. Logs hold titles and file paths, never cookies or sign-in tokens, and are kept for 14 days. Nothing is sent anywhere except to the sites you download from, and to GitHub when a tool is installed or yt-dlp updates itself.

## Uninstalling

Use **Settings > Apps** in Windows. Downora removes the program, its copied tools and logs, and its "start with Windows" entry, then asks whether to delete your settings and history too (the default is to keep them). Your downloaded files are never deleted.
