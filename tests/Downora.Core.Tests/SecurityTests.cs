using Downora.Core.Enums;
using Downora.Core.Models;
using Downora.Core.Services;

namespace Downora.Core.Tests;

public class MediaFileTypesTests
{
    [Theory]
    [InlineData(@"D:\Media\song.mp3")]
    [InlineData(@"D:\Media\clip.MP4")]
    [InlineData(@"D:\Media\a b c.mkv")]
    [InlineData(@"D:\Media\Take 5.flac")]
    [InlineData(@"D:\Media\x.m4a ")]
    public void Known_media_files_are_opened(string path) => Assert.True(MediaFileTypes.IsOpenable(path));

    [Theory]
    [InlineData(@"D:\Media\setup.exe")]
    [InlineData(@"D:\Media\run.bat")]
    [InlineData(@"D:\Media\run.cmd")]
    [InlineData(@"D:\Media\script.ps1")]
    [InlineData(@"D:\Media\script.vbs")]
    [InlineData(@"D:\Media\script.js")]
    [InlineData(@"D:\Media\link.lnk")]
    [InlineData(@"D:\Media\installer.msi")]
    [InlineData(@"D:\Media\page.html")]
    [InlineData(@"D:\Media\page.hta")]
    [InlineData(@"D:\Media\doc.docm")]
    [InlineData(@"D:\Media\song.mp3.exe")] // the real extension is the last one
    [InlineData(@"D:\Media\song")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Anything_else_is_never_opened(string? path) => Assert.False(MediaFileTypes.IsOpenable(path));
}

public class LogSafeTests
{
    [Fact]
    public void A_plain_link_is_kept_and_its_fragment_dropped() =>
        Assert.Equal("https://www.youtube.com/watch?v=jNQXAC9IVRw", LogSafe.Url("https://www.youtube.com/watch?v=jNQXAC9IVRw#t=10"));

    [Theory]
    [InlineData("https://example.com/a?token=abc123&v=1", "https://example.com/a?token=…&v=1")]
    [InlineData("https://example.com/a?X-Amz-Signature=deadbeef&X-Amz-Credential=AKIA", "https://example.com/a?X-Amz-Signature=…&X-Amz-Credential=…")]
    [InlineData("https://example.com/a?SIG=1&sid=2&key=3", "https://example.com/a?SIG=…&sid=…&key=…")]
    [InlineData("https://example.com/a?access%5Ftoken=zzz", "https://example.com/a?access_token=…")]
    public void Token_like_parameters_lose_their_values(string url, string expected) => Assert.Equal(expected, LogSafe.Url(url));

    [Fact]
    public void A_user_name_and_password_never_reach_the_log() =>
        Assert.Equal("https://example.com/private", LogSafe.Url("https://alice:s3cret@example.com/private"));

    [Theory]
    [InlineData("file:///C:/Windows/win.ini")]
    [InlineData("javascript:alert(1)")]
    [InlineData("not a link")]
    [InlineData("")]
    [InlineData(null)]
    public void Anything_that_is_not_a_web_link_is_replaced(string? value) => Assert.Equal("[not a web link]", LogSafe.Url(value));

    [Fact]
    public void A_non_default_port_is_kept() =>
        Assert.Equal("http://example.com:8080/a", LogSafe.Url("http://example.com:8080/a?x=1#frag").Split('?')[0]);
}

public class HostileInputTests
{
    private static readonly UrlValidator Validator = new(new SourceDetector());
    private static readonly string[] WebSchemes = ["http", "https"];

    [Theory]
    [InlineData("https://alice:s3cret@example.com/a.mp4")]
    [InlineData("http://user@example.com/watch")]
    public void Links_with_credentials_are_refused_so_they_are_never_stored_or_logged(string input)
    {
        var result = Validator.Validate(input);

        Assert.Equal(UrlProblem.HasCredentials, result.Problem);
        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("--exec calc")]
    [InlineData("-o /tmp/x")]
    [InlineData("https://example.com/a b")]
    [InlineData("https://example.com/a\nb")]
    [InlineData("https://example.com/a\u0000b")]
    [InlineData("ftp://example.com/a.mp4")]
    [InlineData("file:///C:/Windows/win.ini")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>1</script>")]
    [InlineData("\\\\server\\share\\a.mp4")]
    [InlineData("C:\\Windows\\win.ini")]
    [InlineData("https://")]
    [InlineData("https:///a")]
    [InlineData("localhost")]
    [InlineData("http://localhost")]
    public void Hostile_or_nonsense_input_is_never_valid(string input) => Assert.False(Validator.Validate(input).IsValid);

    [Fact]
    public void An_enormous_input_is_rejected_quickly()
    {
        var result = Validator.Validate("https://example.com/" + new string('a', 200_000));

        Assert.Equal(UrlProblem.TooLong, result.Problem);
    }

    [Fact]
    public void Random_text_never_crashes_the_validator_and_valid_results_are_always_web_links()
    {
        var random = new Random(20261110);
        const string alphabet = "abcXYZ019 :/?#&=.-_%@\\\"'<>|{}[]\t\n\u202e\u200b-";
        for (var i = 0; i < 5000; i++)
        {
            var length = random.Next(0, 120);
            var chars = new char[length];
            for (var c = 0; c < length; c++)
            {
                chars[c] = alphabet[random.Next(alphabet.Length)];
            }

            var result = Validator.Validate(new string(chars));

            if (result.IsValid)
            {
                Assert.Contains(result.Url!.Scheme, WebSchemes);
                Assert.Empty(result.Url.UserInfo);
            }
        }
    }
}

public class FileNameRobustnessTests
{
    private static readonly System.Buffers.SearchValues<char> InvalidChars = System.Buffers.SearchValues.Create("<>:\"/\\|?*");
    private static readonly string[] Reserved = ["CON", "PRN", "AUX", "NUL", "COM1", "COM9", "LPT1", "LPT9"];

    [Fact]
    public void Whatever_a_site_calls_a_video_the_file_name_is_safe()
    {
        var random = new Random(7);

        // Includes lone halves of an emoji (\ud83d, \ude00), which valid JSON can carry.
        var pieces = new[]
        {
            "..", ".", " ", "CON", "nul", "COM1", "LPT9", "aux.txt", "a/b", @"c\d", "x:y", "q?", "*", "\"", "|", "\u202e", "\u200f", "\ufeff",
            "\0", "\n", "\ud83d\ude00", "\ud83d", "\ude00", "é", "e\u0301", "ا", "title", "  ", "...", " .", "{title}", "%s", "%(ext)s",
        };

        for (var i = 0; i < 5000; i++)
        {
            var input = string.Concat(Enumerable.Range(0, random.Next(0, 14)).Select(_ => pieces[random.Next(pieces.Length)]));

            var name = FileNames.Sanitize(input);

            Assert.False(string.IsNullOrWhiteSpace(name));
            Assert.True(name.Length <= FileNames.MaxBaseNameLength + 1); // one more for the "_" put before a reserved name
            Assert.Equal(-1, name.AsSpan().IndexOfAny(InvalidChars));
            Assert.DoesNotContain(name, char.IsControl);
            Assert.False(name.EndsWith('.') || name.EndsWith(' '));
            Assert.NotEqual(".", name);
            Assert.NotEqual("..", name);
            var stem = name.Split('.', 2)[0].TrimEnd();
            Assert.DoesNotContain(stem, Reserved, StringComparer.OrdinalIgnoreCase);
            Assert.Equal(name, Path.GetFileName(name)); // never contains a path
            Assert.Equal(name, name.Normalize()); // valid Unicode all the way
        }
    }

    [Theory]
    [InlineData("..\\..\\Windows\\System32\\evil")]
    [InlineData("../../etc/passwd")]
    [InlineData("C:\\Windows\\win.ini")]
    [InlineData("\\\\server\\share\\x")]
    public void A_title_that_looks_like_a_path_is_flattened_into_one_name(string title)
    {
        var name = FileNames.Expand("{title}", title, null, null);

        Assert.Equal(name, Path.GetFileName(name));
    }

    [Fact]
    public void A_template_cannot_smuggle_a_path_either()
    {
        var name = FileNames.Expand(@"..\{uploader}\{title}", "t", "u", null);

        Assert.Equal(name, Path.GetFileName(name));
    }

    [Theory]
    [InlineData("half \ud83d emoji")]
    [InlineData("\ude00 lone low")]
    [InlineData("ends with high \ud83d")]
    [InlineData("\ud83d\ud83d\ude00")]
    public void Half_an_emoji_in_a_title_does_not_break_naming(string title)
    {
        var name = FileNames.Expand("{title}", title, null, null);

        Assert.False(string.IsNullOrWhiteSpace(name));
        Assert.Equal(name, name.Normalize());
    }

    [Fact]
    public void A_whole_emoji_survives() => Assert.Equal("fun \ud83d\ude00", FileNames.Sanitize("fun \ud83d\ude00"));
}

public class SettingsHardeningTests
{
    [Theory]
    [InlineData(@"C:\Tools\ffmpeg.exe", true)]
    [InlineData(@"C:\Tools\FFMPEG.EXE", true)]
    [InlineData(@"C:\Tools\ffmpeg.bat", false)]
    [InlineData(@"C:\Tools\ffmpeg.ps1", false)]
    [InlineData(@"C:\Tools\ffmpeg", false)]
    [InlineData(@"ffmpeg.exe", false)]
    [InlineData(@"..\ffmpeg.exe", false)]
    public void The_own_ffmpeg_must_be_a_full_path_to_an_exe(string path, bool valid)
    {
        var settings = new AppSettings { DownloadDirectory = @"D:\Downloads", FfmpegPathOverride = path };

        Assert.Equal(valid, AppSettingsValidator.Validate(settings).Count == 0);
    }
}
