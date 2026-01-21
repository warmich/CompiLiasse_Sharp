using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace CompiLiasse_Sharp.Services;

public static class ThemeService
{
    private const string SettingsKey = "AppTheme"; // Default / Light / Dark

    public static ElementTheme LoadTheme()
    {
        var settings = ApplicationData.Current.LocalSettings;
        var value = settings.Values[SettingsKey] as string;

        return value switch
        {
            "Light" => ElementTheme.Light,
            "Dark" => ElementTheme.Dark,
            _ => ElementTheme.Default
        };
    }

    public static void SaveTheme(ElementTheme theme)
    {
        var settings = ApplicationData.Current.LocalSettings;

        var value = theme switch
        {
            ElementTheme.Light => "Light",
            ElementTheme.Dark => "Dark",
            _ => "Default"
        };

        settings.Values[SettingsKey] = value;
    }

    public static void ApplyTheme(Window window, ElementTheme theme)
    {
        // Le thème se met sur l’élément racine de la fenêtre
        if (window.Content is FrameworkElement root)
        {
            root.RequestedTheme = theme;
        }
    }
}
