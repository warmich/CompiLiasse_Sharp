using CompiLiasse_Sharp.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using Windows.Storage.Pickers;
using WinRT.Interop;
using System.Diagnostics;

namespace CompiLiasse_Sharp.Views;

public sealed partial class ItemsPage : Page
{
    private ItemsViewModel VM => (ItemsViewModel)DataContext;

    public ItemsPage()
    {
        InitializeComponent();
        Loaded += ItemsPage_Loaded;
    }

    private async void ItemsPage_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= ItemsPage_Loaded;

        try { await VM.RefreshItemsAsync(); }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
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

    private async void Add_Click(object sender, RoutedEventArgs e)
    {
        try { await VM.AddItemAsync(); }
        catch (Exception ex)
        {
            // pour l’instant : au minimum log + message
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

    // Avant problème de rétention => private void Refresh_Click(object sender, RoutedEventArgs e) => VM.RefreshItems();
    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        try { await VM.RefreshItemsAsync(); }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
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

    private async void DeleteSelected_Click(object sender, RoutedEventArgs e)
    {
        if (VM.SelectedItem is null)
            return;

        var dialog = new ContentDialog
        {
            Title = "Supprimer l’item ?",
            Content = $"Supprimer #{VM.SelectedItem.Id} — {VM.SelectedItem.Name} ?",
            PrimaryButtonText = "Supprimer",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            await VM.DeleteSelectedAsync();
        }
    }

    private async void PickFolder_Click(object sender, RoutedEventArgs e)
    {
        if (VM.SelectedItem is null)
            return;

        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");

        // IMPORTANT en WinUI 3 desktop : init avec le HWND
        var hwnd = WindowNative.GetWindowHandle((Application.Current as App)!.MainWindow!);
        InitializeWithWindow.Initialize(picker, hwnd);

        var folder = await picker.PickSingleFolderAsync();
        if (folder is null)
            return;

        // Sauvegarde en DB
        var db = ((App)Application.Current).Db;
        await db.UpdateItemFolderAsync(VM.SelectedItem.Id, folder.Path);

        // Refresh
        await VM.RefreshItemsAsync();
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var path = VM.SelectedItem?.FolderPath;
        if (string.IsNullOrWhiteSpace(path))
            return;

        // Ouvre l’explorateur Windows sur le dossier
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }

}
