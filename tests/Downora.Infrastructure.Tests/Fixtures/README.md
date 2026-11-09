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

## Download output (Phase 4)

Output of the machine-readable progress templates Downora passes to yt-dlp (`DWN|…` progress, `PP|…` post-processing, `FILE|…` final path):

| File | What it is |
| --- | --- |
| `dl_video_merge.out` | Real. `https://www.youtube.com/watch?v=jNQXAC9IVRw`, video + audio streams merged (`-f "bv*+ba/b"`) |
| `dl_audio_extract.out` | Real. The same link as audio only (`-x --audio-format mp3`) |
| `dl_fragments.out` | Hand-made from the same format. A segmented (HLS) download whose size is unknown, plus one stray non-progress line |

The `FILE|` paths were replaced with `C:\Downloads\…`; tests that need a real file substitute their own.
