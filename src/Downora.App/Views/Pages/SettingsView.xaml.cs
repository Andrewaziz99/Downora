using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Downora.Presentation.ViewModels;

namespace Downora.App.Views.Pages;

public partial class SettingsView : UserControl
{
    private SettingsViewModel? _model;

    public SettingsView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += (_, _) => RevealCookieOptionsIfRequested();
        Unloaded += (_, _) => Detach();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        Detach();
        _model = e.NewValue as SettingsViewModel;
        if (_model is not null)
        {
            _model.CookieOptionsRevealRequested += OnRevealRequested;
        }
    }

    private void Detach()
    {
        if (_model is not null)
        {
            _model.CookieOptionsRevealRequested -= OnRevealRequested;
        }
    }

    private void OnRevealRequested(object? sender, EventArgs e) => RevealCookieOptionsIfRequested();

    /// <summary>Scrolls the browser sign-in option into view when another screen sent the user here for it.</summary>
    private void RevealCookieOptionsIfRequested()
    {
        if (_model is null || !IsLoaded)
        {
            return;
        }

        // Layout may not be final yet when the page has just been shown, so wait until it is.
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            if (_model?.ConsumeCookieReveal() == true)
            {
                CookieSection.BringIntoView();
                CookieToggle.Focus();
            }
        });
    }
}
