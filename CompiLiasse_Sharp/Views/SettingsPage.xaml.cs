using CompiLiasse_Sharp.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using Windows.Storage.Pickers;
using WinRT.Interop;
using Windows.Storage;

namespace CompiLiasse_Sharp.Views;

public sealed partial class SettingsPage : Page
{
    private SettingsProfilesViewModel VM => (SettingsProfilesViewModel)DataContext;

    public SettingsPage()
    {
        InitializeComponent();
        // DataContext déjà assigné chez toi (en code-behind)
        this.Loaded += SettingsPage_Loaded;

        // Récupère la MainWindow en cours
        var window = (Microsoft.UI.Xaml.Application.Current as App)?.MainWindow as MainWindow;

        if (window != null)
        {
            DataContext = new SettingsProfilesViewModel(window);
            //DataContext = new SettingsViewModel(window);
        }
    }

    private async void SettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        this.Loaded -= SettingsPage_Loaded;

        if (DataContext is SettingsProfilesViewModel vm)
        {
            try { await vm.LoadAsync(); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }
    }

    private async void ExcelPath_LostFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsProfilesViewModel vm)
            await vm.OnExcelLostFocusAsync();
    }

    private async void FolderPath_LostFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsProfilesViewModel vm)
            await vm.OnFolderLostFocusAsync();
    }

    private async void SaveAs_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsProfilesViewModel vm)
            return;

        var input = new TextBox
        {
            PlaceholderText = "Nom de la configuration…"
        };

        var dlg = new ContentDialog
        {
            Title = "Enregistrer sous…",
            Content = input,
            PrimaryButtonText = "Enregistrer",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };

        var result = await dlg.ShowAsync();
        if (result != ContentDialogResult.Primary)
            return;

        var saveResult = await vm.SaveAsAsync(input.Text);
        if (!saveResult.Success)
        {
            var errDlg = new ContentDialog
            {
                Title = "Impossible",
                Content = saveResult.Error ?? "Erreur",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await errDlg.ShowAsync();
        }
    }

    private async void AddProfile_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsProfilesViewModel vm)
            return;

        var input = new TextBox { PlaceholderText = "Nom de la configuration…" };

        var dlg = new ContentDialog
        {
            Title = "Nouvelle configuration",
            Content = input,
            PrimaryButtonText = "Créer",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };

        var result = await dlg.ShowAsync();
        if (result != ContentDialogResult.Primary)
            return;

        var (ok, error) = await vm.AddProfileAsync(input.Text);
        if (!ok)
        {
            var errDlg = new ContentDialog
            {
                Title = "Impossible",
                Content = error ?? "Erreur",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await errDlg.ShowAsync();
        }

    }

    private async void DeactivateProfile_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsProfilesViewModel vm)
            return;

        if (vm.SelectedProfile is null)
            return;

        // Message adapté
        var name = vm.SelectedProfile.Name;

        var confirm = new ContentDialog
        {
            Title = "Désactiver la configuration ?",
            Content = $"Désactiver “{name}” ?\nElle restera en base mais ne sera plus proposée.",
            PrimaryButtonText = "Désactiver",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };

        var res = await confirm.ShowAsync();
        if (res != ContentDialogResult.Primary)
            return;

        var (ok, error) = await vm.DeactivateSelectedAsync();
        if (!ok)
        {
            var errDlg = new ContentDialog
            {
                Title = "Impossible",
                Content = error ?? "Erreur",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await errDlg.ShowAsync();
        }

    }

    private async void BrowseExcel_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsProfilesViewModel vm)
            return;

        try
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".xlsx");
            picker.FileTypeFilter.Add(".xlsm");
            picker.FileTypeFilter.Add(".xls");

            InitializeWithWindow.Initialize(picker, GetHwnd());

            StorageFile? file = await picker.PickSingleFileAsync();
            if (file is null)
                return;

            // 1) Injecte dans le VM (déclenche validation + draft)
            vm.ExcelPathText = file.Path;

            // 2) Sauve en DB immédiatement (même règle que LostFocus)
            await vm.OnExcelLostFocusAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);

            var dlg = new ContentDialog
            {
                Title = "Erreur",
                Content = ex.Message,
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await dlg.ShowAsync();
        }
    }

    private async void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsProfilesViewModel vm)
            return;

        try
        {
            var picker = new FolderPicker();
            picker.FileTypeFilter.Add("*"); // requis même si folder picker

            InitializeWithWindow.Initialize(picker, GetHwnd());

            StorageFolder? folder = await picker.PickSingleFolderAsync();
            if (folder is null)
                return;

            // 1) Injecte dans le VM (déclenche validation + draft)
            vm.FolderPathText = folder.Path;

            // 2) Sauve en DB immédiatement (même règle que LostFocus)
            await vm.OnFolderLostFocusAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);

            var dlg = new ContentDialog
            {
                Title = "Erreur",
                Content = ex.Message,
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await dlg.ShowAsync();
        }
    }

    private IntPtr GetHwnd()
    {
        var window = (Microsoft.UI.Xaml.Application.Current as App)?.MainWindow;
        return WindowNative.GetWindowHandle(window);
    }

    private const double ProfileRowWideMinWidth = 620; // ajuste si besoin

    private void ProfileRowRoot_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateProfileRowLayout(ProfileRowRoot.ActualWidth);
    }

    private void ProfileRowRoot_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateProfileRowLayout(e.NewSize.Width);
    }

    private void UpdateProfileRowLayout(double width)
    {
        var wide = width >= ProfileRowWideMinWidth;

        ProfileRowWide.Visibility = wide ? Visibility.Visible : Visibility.Collapsed;
        ProfileRowNarrow.Visibility = wide ? Visibility.Collapsed : Visibility.Visible;
    }

}
