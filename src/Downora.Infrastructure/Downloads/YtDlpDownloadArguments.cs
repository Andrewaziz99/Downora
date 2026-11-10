using System.Globalization;
using Downora.Core.Enums;
using Downora.Core.Models;
using Downora.Core.Services;

namespace Downora.Infrastructure.Downloads;

/// <summary>Where and how a download is laid out on disk; computed once per run.</summary>
internal sealed record DownloadLayout(
    string FfmpegPath,
    string? DenoPath,
    string OutputDirectory,
    string TempDirectory,
    string BaseName);

/// <summary>
/// Builds yt-dlp's argument list for one download. Pure: arguments are returned one by one and passed through
/// <c>ProcessStartInfo.ArgumentList</c>, never joined into a command line, and the URL always follows "--".
/// </summary>
internal static class YtDlpDownloadArguments
{
    public static IReadOnlyList<string> Build(DownloadRequest request, DownloadLayout layout)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(layout);
        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var url) || url.Scheme is not ("http" or "https"))
        {
            throw new ArgumentException("Only http and https links can be downloaded.", nameof(request));
        }

        var args = new List<string>
        {
            "--ignore-config", // a user's yt-dlp.conf must not change what Downora runs
            "--no-plugin-dirs", // nor may code from a plugin folder that anything could have written to
            "--no-playlist",
            "--newline",
            "--progress", // progress is printed even though --print makes yt-dlp otherwise quiet
            "--color", "never",
            "--no-mtime", // the file's date is when it was downloaded, not when it was uploaded
            "--windows-filenames",
            "--socket-timeout", "20",
            "--retries", "5",
            "--fragment-retries", "5",
            "--concurrent-fragments", "4",
        };

        if (request.Kind == MediaKind.Audio)
        {
            AddAudio(args, request);
        }
        else
        {
            AddVideo(args, request);
        }

        AddTags(args, request);

        args.Add("--ffmpeg-location");
        args.Add(layout.FfmpegPath);

        if (layout.DenoPath is not null)
        {
            // YouTube needs a JavaScript runtime for its challenges (yt-dlp ≥ 2025.11).
            args.Add("--js-runtimes");
            args.Add($"deno:{layout.DenoPath}");
        }

        if (request.CookieBrowser is not null)
        {
            if (!CookieBrowsers.IsSupported(request.CookieBrowser))
            {
                throw new ArgumentException($"'{request.CookieBrowser}' is not a supported browser.", nameof(request));
            }

            args.Add("--cookies-from-browser");
            args.Add(request.CookieBrowser);
        }

        // Partial and intermediate files live in a folder of their own, so cancelling cleans up by deleting one folder.
        args.Add("--paths");
        args.Add("home:" + layout.OutputDirectory);
        args.Add("--paths");
        args.Add("temp:" + layout.TempDirectory);
        args.Add("-o");
        args.Add(OutputTemplate(layout.BaseName));

        args.Add("--progress-template");
        args.Add(YtDlpProgressParser.DownloadTemplate);
        args.Add("--progress-template");
        args.Add(YtDlpProgressParser.PostProcessTemplate);
        args.Add("--print");
        args.Add(YtDlpProgressParser.FileTemplate);

        // Everything after "--" is a URL, never an option, whatever it contains.
        args.Add("--");
        args.Add(url.AbsoluteUri);
        return args;
    }

    private static void AddTags(List<string> args, DownloadRequest request)
    {
        if (request.EmbedMetadata)
        {
            args.Add("--embed-metadata");
        }

        if (request.EmbedCoverArt && CanHoldCoverArt(request))
        {
            // Thumbnails often come as WebP, which players don't read as cover art, so they become JPEG first.
            args.Add("--convert-thumbnails");
            args.Add("jpg");
            args.Add("--embed-thumbnail");
        }
    }

    /// <summary>WAV has no cover picture and WebM can't carry attachments; asking for one would only produce a warning.</summary>
    internal static bool CanHoldCoverArt(DownloadRequest request) => request.Kind == MediaKind.Audio
        ? request.AudioFormat != AudioFormat.Wav
        : request.Container != VideoContainer.WebM;

    /// <summary>The <c>-o</c> template: our own sanitized name, with only the extension left for yt-dlp to fill in.</summary>
    internal static string OutputTemplate(string baseName) =>
        baseName.Replace("%", "%%", StringComparison.Ordinal) + ".%(ext)s";

    private static void AddVideo(List<string> args, DownloadRequest request)
    {
        // Best video + best audio, or the best single file when the site only has combined formats.
        args.Add("-f");
        args.Add("bv*+ba/b");

        // "res" is the shorter side, the same measure the quality list used, so vertical videos pick the right row.
        // After resolution and frame rate, prefer codecs an MP4 can hold so the file doesn't have to become MKV.
        var sort = new List<string> { ResolutionSort(request.Quality), "fps" };
        if (request.Container == VideoContainer.Mp4)
        {
            sort.Add("vcodec:h264");
            sort.Add("acodec:aac");
        }

        args.Add("-S");
        args.Add(string.Join(',', sort));

        args.Add("--merge-output-format");
        args.Add(request.Container switch
        {
            VideoContainer.Mkv => "mkv",
            VideoContainer.WebM => "webm/mkv",
            _ => "mp4/mkv", // yt-dlp takes the first container that can hold the chosen codecs
        });
    }

    private static void AddAudio(List<string> args, DownloadRequest request)
    {
        args.Add("-f");
        args.Add(request.AudioFormat switch
        {
            // Pick a stream that is already in the target codec when there is one, to avoid converting twice.
            AudioFormat.M4a => "ba[ext=m4a]/ba/b",
            AudioFormat.Opus => "ba[acodec=opus]/ba/b",
            _ => "ba/b",
        });

        args.Add("-x");
        args.Add("--audio-format");
        args.Add(AudioFormatName(request.AudioFormat));

        if (request.AudioFormat is AudioFormat.Mp3 or AudioFormat.M4a or AudioFormat.Opus)
        {
            args.Add("--audio-quality");
            args.Add(string.Create(CultureInfo.InvariantCulture, $"{request.AudioBitrateKbps}K"));
        }
    }

    /// <summary>"res:1080" prefers the best stream up to 1080; Best and 4K take the highest there is.</summary>
    internal static string ResolutionSort(QualityPreset quality) => quality switch
    {
        QualityPreset.P1440 => "res:1440",
        QualityPreset.P1080 => "res:1080",
        QualityPreset.P720 => "res:720",
        QualityPreset.P480 => "res:480",
        QualityPreset.P360 => "res:360",
        _ => "res",
    };

    internal static string AudioFormatName(AudioFormat format) => format switch
    {
        AudioFormat.M4a => "m4a",
        AudioFormat.Wav => "wav",
        AudioFormat.Flac => "flac",
        AudioFormat.Opus => "opus",
        _ => "mp3",
    };
}
