using Downora.App.ViewModels;
using Wpf.Ui.Controls;

namespace Downora.App.Views;

public partial class MainWindow : FluentWindow
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
