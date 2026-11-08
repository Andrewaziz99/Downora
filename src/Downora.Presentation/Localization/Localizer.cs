using System.ComponentModel;
using System.Globalization;
using System.Resources;
using Downora.Core.Enums;

namespace Downora.Presentation.Localization;

/// <summary>
/// UI strings for the current language. XAML binds to the indexer (<c>Localizer.Instance[key]</c>);
/// changing the language raises <c>Item[]</c> so every bound label updates in place.
/// </summary>
public sealed class Localizer : INotifyPropertyChanged
{
    private const string IndexerName = "Item[]";

    private static readonly ResourceManager Strings =
        new("Downora.Presentation.Localization.Strings", typeof(Localizer).Assembly);

    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");
    private static readonly CultureInfo Arabic = CultureInfo.GetCultureInfo("ar-EG");

    /// <summary>The app-wide instance; XAML can't use constructor injection, so it reads this.</summary>
    public static Localizer Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? LanguageChanged;

    public AppLanguage Language { get; private set; } = AppLanguage.English;

    public CultureInfo Culture { get; private set; } = English;

    public bool IsRightToLeft => Language == AppLanguage.Arabic;

    /// <summary>The string for <paramref name="key"/>, or <c>[key]</c> so a missing translation is visible instead of blank.</summary>
    public string this[string key] =>
        Strings.GetString(key, Culture) ?? $"[{key}]";

    public string Format(string key, params object?[] args) =>
        string.Format(Culture, this[key], args);

    public void SetLanguage(AppLanguage language)
    {
        var culture = language == AppLanguage.Arabic ? Arabic : English;
        var changed = language != Language;

        Language = language;
        Culture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;

        if (!changed)
        {
            return;
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(IndexerName));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Culture)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRightToLeft)));
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }
}
