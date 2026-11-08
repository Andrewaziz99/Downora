using System.Windows.Data;
using System.Windows.Markup;
using Downora.Presentation.Localization;

namespace Downora.App.Localization;

/// <summary>
/// <c>Text="{l:Loc Nav_Home}"</c>: a live binding to the localized string, so switching language updates the UI without a restart.
/// </summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class LocExtension : MarkupExtension
{
    public LocExtension()
    {
    }

    public LocExtension(string key) => Key = key;

    [ConstructorArgument("key")]
    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding($"[{Key}]") { Source = Localizer.Instance, Mode = BindingMode.OneWay };
        return binding.ProvideValue(serviceProvider);
    }
}
