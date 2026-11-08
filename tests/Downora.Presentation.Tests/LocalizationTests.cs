using System.Text.RegularExpressions;
using System.Xml.Linq;
using Downora.Core.Enums;
using Downora.Presentation.Localization;

namespace Downora.Presentation.Tests;

[Collection(CultureSensitive.Name)]
public partial class LocalizationTests
{
    private static string ResxPath(string file)
    {
        // Walk up from the test output folder to the repository root.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Downora.sln")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return Path.Combine(dir.FullName, "src", "Downora.Presentation", "Localization", file);
    }

    private static Dictionary<string, string> Load(string file) =>
        XDocument.Load(ResxPath(file)).Root!.Elements("data")
            .ToDictionary(e => (string)e.Attribute("name")!, e => (string?)e.Element("value") ?? string.Empty);

    [GeneratedRegex(@"\{\d+\}")]
    private static partial Regex Placeholder();

    [Fact]
    public void English_and_Arabic_have_the_same_keys()
    {
        var en = Load("Strings.resx");
        var ar = Load("Strings.ar.resx");

        Assert.Empty(en.Keys.Except(ar.Keys));
        Assert.Empty(ar.Keys.Except(en.Keys));
    }

    [Fact]
    public void No_translation_is_blank_and_placeholders_match()
    {
        var en = Load("Strings.resx");
        var ar = Load("Strings.ar.resx");

        foreach (var (key, english) in en)
        {
            Assert.False(string.IsNullOrWhiteSpace(english), $"English '{key}' is blank");
            Assert.False(string.IsNullOrWhiteSpace(ar[key]), $"Arabic '{key}' is blank");

            var enHoles = Placeholder().Matches(english).Select(m => m.Value).Order();
            var arHoles = Placeholder().Matches(ar[key]).Select(m => m.Value).Order();
            Assert.True(enHoles.SequenceEqual(arHoles), $"'{key}' has different {{n}} placeholders in Arabic");
        }
    }

    [Fact]
    public void Every_download_status_has_a_label()
    {
        var en = Load("Strings.resx");
        foreach (var status in Enum.GetValues<DownloadStatus>())
        {
            Assert.True(en.ContainsKey($"Status_{status}"), $"Missing Status_{status}");
        }
    }

    [Fact]
    public void Switching_to_Arabic_translates_turns_on_rtl_and_notifies_bindings()
    {
        var localizer = new Localizer();
        var changed = new List<string?>();
        localizer.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        try
        {
            Assert.Equal("Settings", localizer["Nav_Settings"]);
            Assert.False(localizer.IsRightToLeft);

            localizer.SetLanguage(AppLanguage.Arabic);

            Assert.Equal("الإعدادات", localizer["Nav_Settings"]);
            Assert.True(localizer.IsRightToLeft);
            Assert.Contains("Item[]", changed);
            Assert.Equal("ar", localizer.Culture.TwoLetterISOLanguageName);
        }
        finally
        {
            localizer.SetLanguage(AppLanguage.English);
        }
    }

    [Fact]
    public void Setting_the_same_language_again_raises_nothing()
    {
        var localizer = new Localizer();
        var raised = 0;
        localizer.LanguageChanged += (_, _) => raised++;

        localizer.SetLanguage(AppLanguage.English);

        Assert.Equal(0, raised);
    }

    [Fact]
    public void Missing_keys_are_visible_not_blank() =>
        Assert.Equal("[No_Such_Key]", new Localizer()["No_Such_Key"]);

    [Fact]
    public void Format_fills_placeholders() =>
        Assert.Equal("Version 1.2.3", new Localizer().Format("About_Version", "1.2.3"));
}
