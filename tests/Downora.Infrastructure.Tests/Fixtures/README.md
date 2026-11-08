# yt-dlp fixtures

Real output of yt-dlp 2026.10.26 (with Deno), captured while building Phase 3:

| File | Command |
| --- | --- |
| `yt_video.json` | `yt-dlp -J --flat-playlist --no-playlist https://www.youtube.com/watch?v=jNQXAC9IVRw` |
| `sc_track.json` | `yt-dlp -J --flat-playlist https://soundcloud.com/forss/flickermood` |
| `yt_playlist.json` | `yt-dlp -J --flat-playlist https://www.youtube.com/playlist?list=PLlaN88a7y2_plecYoJxvRFTLHVbIVAOoc` |
| `*.err` | stderr of failed runs (bot check, removed video, 404, SoundCloud sign-in warning) |

Signed media URLs, HTTP headers, fragments and long text fields were removed to keep the files small.
The playlist keeps its first 6 entries plus one synthetic `[Private video]` entry to cover unavailable items.
