using CompiLiasse_Sharp.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace CompiLiasse_Sharp.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();

        // Récupère la MainWindow en cours
        var window = (Microsoft.UI.Xaml.Application.Current as App)?.MainWindow as MainWindow;

        if (window != null)
        {
            DataContext = new SettingsViewModel(window);
        }
    }
}
