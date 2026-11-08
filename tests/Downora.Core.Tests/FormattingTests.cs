using System.Globalization;
using Downora.Core.Enums;
using Downora.Core.Services;

namespace Downora.Core.Tests;

public class FormattingTests
{
    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");

    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(999, "999 B")]
    [InlineData(1024, "1 KB")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(88_400_000, "84.3 MB")]
    [InlineData(2_576_980_378, "2.4 GB")]
    [InlineData(443_600_000_000, "413 GB")]
    [InlineData(-1, "—")]
    public void Bytes_use_binary_units(long bytes, string expected) =>
        Assert.Equal(expected, Formatting.Bytes(bytes, En));

    [Fact]
    public void Bytes_follow_the_culture_decimal_separator() =>
        Assert.Equal("1,5 KB", Formatting.Bytes(1536, CultureInfo.GetCultureInfo("de-DE")));

    [Fact]
    public void Speed_appends_per_second() =>
        Assert.Equal("18.4 MB/s", Formatting.Speed(18.4 * 1024 * 1024, En));

    [Theory]
    [InlineData(245, "4:05")]
    [InlineData(59, "0:59")]
    [InlineData(3729, "1:02:09")]
    public void Duration_is_compact(int seconds, string expected) =>
        Assert.Equal(expected, Formatting.Duration(TimeSpan.FromSeconds(seconds), En));

    [Fact]
    public void Unknown_duration_is_null() => Assert.Null(Formatting.Duration(null));

    [Theory]
    [InlineData(MediaKind.Video, VideoContainer.Mp4, null, "MP4")]
    [InlineData(MediaKind.Video, null, null, "MP4")]
    [InlineData(MediaKind.Video, VideoContainer.Mkv, null, "MKV")]
    [InlineData(MediaKind.Audio, null, AudioFormat.Flac, "FLAC")]
    [InlineData(MediaKind.Audio, null, null, "MP3")]
    public void File_type_badge(MediaKind kind, VideoContainer? container, AudioFormat? audio, string expected) =>
        Assert.Equal(expected, Formatting.FileType(kind, container, audio));

    [Theory]
    [InlineData(QualityPreset.P2160, "4K")]
    [InlineData(QualityPreset.P1080, "1080p")]
    [InlineData(QualityPreset.AudioOnly, "Audio")]
    public void Quality_labels(QualityPreset preset, string expected) =>
        Assert.Equal(expected, Formatting.Quality(preset));
}
