using System.Windows;
using System.Windows.Markup;
using Downora.Presentation.Localization;
using Downora.Presentation.ViewModels;
using Wpf.Ui.Controls;

namespace Downora.App.Views;

public partial class MainWindow : FluentWindow
{
    private readonly Localizer _localizer;

    public MainWindow(ShellViewModel viewModel, Localizer localizer)
    {
        _localizer = localizer;
        DataContext = viewModel;
        InitializeComponent();

        ApplyLanguage();
        _localizer.LanguageChanged += (_, _) => ApplyLanguage();
    }

    /// <summary>Arabic mirrors the whole window (sidebar on the right) and uses Arabic text shaping.</summary>
    private void ApplyLanguage()
    {
        FlowDirection = _localizer.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        Language = XmlLanguage.GetLanguage(_localizer.Culture.IetfLanguageTag);
    }
}
