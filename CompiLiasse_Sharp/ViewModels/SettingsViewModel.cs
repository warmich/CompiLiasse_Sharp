using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using CompiLiasse_Sharp.Services;
using Windows.ApplicationModel;

namespace CompiLiasse_Sharp.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly MainWindow _mainWindow;

    // 0 = Default, 1 = Light, 2 = Dark
    private int _themeIndex;
    public int ThemeIndex
    {
        get => _themeIndex;
        set
        {
            if (!SetProperty(ref _themeIndex, value))
                return;

            var theme = value switch
            {
                1 => ElementTheme.Light,
                2 => ElementTheme.Dark,
                _ => ElementTheme.Default
            };

            ThemeService.SaveTheme(theme);
            ThemeService.ApplyTheme(_mainWindow, theme);

            // Important : recalcule aussi les boutons système de la titlebar
            _mainWindow.RefreshCaptionButtons();
        }
    }
    public string AppVersion
    {
        get
        {
            var v = Package.Current.Id.Version;
            return $"{v.Major}.{v.Minor}.{v.Build}.{v.Revision}";
        }
    }

    public string PackageName => Package.Current.DisplayName;

    public SettingsViewModel(MainWindow mainWindow)
    {
        _mainWindow = mainWindow;

        var currentTheme = ThemeService.LoadTheme();
        ThemeIndex = currentTheme switch
        {
            ElementTheme.Light => 1,
            ElementTheme.Dark => 2,
            _ => 0
        };
    }
}
