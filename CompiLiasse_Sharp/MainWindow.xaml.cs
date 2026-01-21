using CompiLiasse_Sharp.Services;
using CompiLiasse_Sharp.Views;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using WinRT.Interop;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CompiLiasse_Sharp
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private AppWindow? _appWindow;
        private AppWindowTitleBar? _titleBar;
        private bool _isClosing;


        public MainWindow()
        {
            InitializeComponent();

            Closed += (_, __) =>
            {
                _isClosing = true;
                Activated -= OnWindowActivated;
            };

            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);

            // Récupère AppWindow + TitleBar
            _appWindow = GetAppWindow();
            _titleBar = _appWindow?.TitleBar;

            // Applique le thème sauvegardé
            var theme = ThemeService.LoadTheme();
            ThemeService.ApplyTheme(this, theme);

            // Applique la couleur des boutons (actif au démarrage)
            UpdateCaptionButtonsColors(theme, isActive: true);

            // Met à jour quand la fenêtre devient active/inactive
            Activated += OnWindowActivated;

            RootFrame.Navigate(typeof(ShellPage));
        }

        private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
        {
            if (_isClosing) return;

            try
            {
                bool isActive = args.WindowActivationState != WindowActivationState.Deactivated;
                var currentTheme = GetCurrentTheme();
                UpdateCaptionButtonsColors(currentTheme, isActive);
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                // Fenêtre déjà fermée (arrêt debug / fermeture rapide) -> on ignore
            }
            catch (InvalidOperationException)
            {
                // Idem : état invalide lors de la fermeture -> on ignore
            }
        }

        private AppWindow? GetAppWindow()
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            return AppWindow.GetFromWindowId(windowId);
        }

        private ElementTheme GetCurrentTheme()
        {
            if (_isClosing) return ElementTheme.Default;

            try
            {
                if (Content is FrameworkElement root)
                    return root.ActualTheme;
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                // Window déjà fermée
            }

            return ElementTheme.Default;
        }

        // Appelle ça après un changement de thème depuis Settings
        public void RefreshCaptionButtons()
        {
            var theme = GetCurrentTheme();
            UpdateCaptionButtonsColors(theme, isActive: true);
        }

        private void UpdateCaptionButtonsColors(ElementTheme theme, bool isActive)
        {
            if (_titleBar is null) return;

            // Dans ton SDK, IsCustomizationSupported est une MÉTHODE
            if (!AppWindowTitleBar.IsCustomizationSupported())
                return;

            // Objectif demandé :
            // - thème clair : actif = foncé, inactif = gris
            // - thème sombre : actif = clair, inactif = gris
            bool isDark = theme == ElementTheme.Dark;

            Color activeFg = isDark
                ? Color.FromArgb(255, 240, 240, 240) // clair
                : Color.FromArgb(255, 30, 30, 30);   // foncé (thème clair)

            Color inactiveFg = Color.FromArgb(255, 160, 160, 160); // gris lisible

            _titleBar.ButtonForegroundColor = isActive ? activeFg : inactiveFg;
            _titleBar.ButtonInactiveForegroundColor = inactiveFg;

            // Bonus : hover/pressed pour une meilleure lisibilité
            _titleBar.ButtonHoverForegroundColor = activeFg;
            _titleBar.ButtonPressedForegroundColor = activeFg;

            // Fond transparent (Mica derrière)
            _titleBar.ButtonBackgroundColor = Colors.Transparent;
            _titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            _titleBar.ButtonHoverBackgroundColor = Color.FromArgb(18, 255, 255, 255);
            _titleBar.ButtonPressedBackgroundColor = Color.FromArgb(30, 255, 255, 255);
        }
    }
}
