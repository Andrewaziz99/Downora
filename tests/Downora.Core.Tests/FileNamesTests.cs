using Downora.Core.Services;

namespace Downora.Core.Tests;

public class FileNamesTests
{
    [Theory]
    [InlineData("Plain title", "Plain title")]
    [InlineData("AC/DC: Back In Black (Live)", "AC DC Back In Black (Live)")]
    [InlineData("What? <Why> \"now\" | * \\ /", "What Why now")]
    [InlineData("  spaced   out   ", "spaced out")]
    [InlineData("ends with dots...", "ends with dots")]
    [InlineData("ends with dot and space . ", "ends with dot and space")]
    [InlineData("tab\tand\nnewline", "tab and newline")]
    [InlineData("bell\a here", "bell here")]
    [InlineData("عنوان عربي: مقطع", "عنوان عربي مقطع")]
    [InlineData("日本語のタイトル", "日本語のタイトル")]
    public void Characters_Windows_rejects_are_replaced(string input, string expected) =>
        Assert.Equal(expected, FileNames.Sanitize(input));

    [Theory]
    [InlineData("CON", "_CON")]
    [InlineData("con", "_con")]
    [InlineData("NUL", "_NUL")]
    [InlineData("com1", "_com1")]
    [InlineData("LPT9", "_LPT9")]
    [InlineData("PRN.txt", "_PRN.txt")]
    [InlineData("AUX.final.mp3", "_AUX.final.mp3")]
    [InlineData("CONSOLE", "CONSOLE")]
    [InlineData("COM10", "COM10")]
    [InlineData("Nullable", "Nullable")]
    public void Reserved_device_names_are_renamed_but_look_alikes_are_left_alone(string input, string expected) =>
        Assert.Equal(expected, FileNames.Sanitize(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("...")]
    [InlineData("???")]
    [InlineData("<>:\"/\\|?*")]
    public void Nothing_usable_becomes_a_fallback_name(string? input) => Assert.Equal("download", FileNames.Sanitize(input));

    [Fact]
    public void Long_titles_are_cut_to_the_limit()
    {
        var result = FileNames.Sanitize(new string('a', 500));
        Assert.Equal(FileNames.MaxBaseNameLength, result.Length);
    }

    [Fact]
    public void Cutting_never_splits_an_emoji_in_half()
    {
        // 119 letters then an emoji (two UTF-16 units): the limit falls in the middle of it.
        var result = FileNames.Sanitize(new string('a', FileNames.MaxBaseNameLength - 1) + "😀 more");

        Assert.Equal(FileNames.MaxBaseNameLength - 1, result.Length);
        Assert.DoesNotContain(result, char.IsSurrogate);
    }

    [Fact]
    public void Cutting_does_not_leave_a_trailing_space_or_dot()
    {
        var result = FileNames.Sanitize(new string('a', FileNames.MaxBaseNameLength - 1) + " tail");
        Assert.Equal(new string('a', FileNames.MaxBaseNameLength - 1), result);
    }

    [Fact]
    public void Invisible_direction_marks_are_removed()
    {
        // A right-to-left override can make "evil‮gnp.exe" display as "evilexe.png".
        var result = FileNames.Sanitize("evil‮gnp.exe‏");

        Assert.DoesNotContain('‮', result);
        Assert.DoesNotContain('‏', result);
    }

    [Fact]
    public void Nothing_returned_contains_a_character_windows_forbids()
    {
        var hostile = string.Concat(Enumerable.Range(0, 0x80).Select(c => (char)c)) + "end";
        var result = FileNames.Sanitize(hostile);

        Assert.Equal(-1, result.IndexOfAny(Path.GetInvalidFileNameChars()));
        Assert.EndsWith("end", result, StringComparison.Ordinal);
    }

    // ---- Templates ----

    [Fact]
    public void Templates_fill_in_the_known_tokens()
    {
        Assert.Equal("jawed - Me at the zoo [360p]", FileNames.Expand("{uploader} - {title} [{quality}]", "Me at the zoo", "jawed", "360p"));
        Assert.Equal("Me at the zoo", FileNames.Expand("{TITLE}", "Me at the zoo", null, null));
    }

    [Fact]
    public void Unknown_tokens_and_missing_values_leave_nothing_behind()
    {
        Assert.Equal("Song", FileNames.Expand("{title}{surprise}", "Song", null, null));
        Assert.Equal("Song -", FileNames.Expand("{title} - {uploader}", "Song", null, null));
        Assert.Equal("download", FileNames.Expand("{nothing}", "Song", null, null));
    }

    [Fact]
    public void The_default_template_is_just_the_title_and_the_result_is_sanitized()
    {
        Assert.Equal("Me at the zoo", FileNames.Expand(null, "Me at the zoo", "jawed", "360p"));
        Assert.Equal("A B", FileNames.Expand("", "A:B", null, null));
        Assert.Equal("_CON", FileNames.Expand("{title}", "CON", null, null));
        Assert.Equal("a b", FileNames.Expand("{title}", "a/b", null, null));
    }

    [Fact]
    public void A_path_in_a_title_cannot_escape_the_download_folder()
    {
        var result = FileNames.Expand("{title}", @"..\..\Windows\System32\evil", null, null);

        Assert.DoesNotContain('\\', result);
        Assert.DoesNotContain('/', result);
        Assert.Equal(-1, result.IndexOfAny(Path.GetInvalidFileNameChars()));
    }

    // ---- Uniqueness ----

    [Fact]
    public void A_free_name_is_used_as_is()
    {
        Assert.Equal("Song", FileNames.MakeUnique("Song", _ => false));
    }

    [Fact]
    public void A_taken_name_gets_the_next_free_number()
    {
        HashSet<string> taken = ["Song", "Song (2)", "Song (3)"];

        Assert.Equal("Song (4)", FileNames.MakeUnique("Song", taken.Contains));
        Assert.Equal("Other", FileNames.MakeUnique("Other", taken.Contains));
    }
}
