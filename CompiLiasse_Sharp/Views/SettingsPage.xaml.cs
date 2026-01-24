using CompiLiasse_Sharp.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

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

    private void ExcelPath_LostFocus(object sender, RoutedEventArgs e)
        => VM.OnExcelLostFocus();

    private void FolderPath_LostFocus(object sender, RoutedEventArgs e)
        => VM.OnFolderLostFocus();

    private async void SaveAs_Click(object sender, RoutedEventArgs e)
    {
        var tb = new TextBox { PlaceholderText = "Nom de la nouvelle configuration..." };

        var dlg = new ContentDialog
        {
            Title = "Enregistrer sous",
            Content = tb,
            PrimaryButtonText = "Enregistrer",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };

        var result = await dlg.ShowAsync();
        if (result != ContentDialogResult.Primary)
            return;

        if (!VM.SaveAs(tb.Text, out var error))
        {
            var err = new ContentDialog
            {
                Title = "Erreur",
                Content = error ?? "Impossible d'enregistrer.",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await err.ShowAsync();
        }
    }

    private async void AddProfile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new ContentDialog
        {
            Title = "Info",
            Content = "Ajout/Suppression seront branchés à la DB à l'étape 6.",
            CloseButtonText = "OK",
            XamlRoot = this.XamlRoot
        };
        await dlg.ShowAsync();
    }

    private async void BrowseExcel_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new ContentDialog
        {
            Title = "Info",
            Content = "Le picker Excel sera implémenté à l'étape 5.",
            CloseButtonText = "OK",
            XamlRoot = this.XamlRoot
        };
        await dlg.ShowAsync();
    }

    private async void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new ContentDialog
        {
            Title = "Info",
            Content = "Le picker dossier sera implémenté à l'étape 5.",
            CloseButtonText = "OK",
            XamlRoot = this.XamlRoot
        };
        await dlg.ShowAsync();
    }
}
