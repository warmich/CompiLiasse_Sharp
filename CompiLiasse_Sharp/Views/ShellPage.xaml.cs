using CompiLiasse_Sharp.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace CompiLiasse_Sharp.Views;

public sealed partial class ShellPage : Page
{
    private ShellViewModel VM => (ShellViewModel)DataContext;

    public ShellPage()
    {
        InitializeComponent();

        // Abonnement unique
        ContentFrame.Navigated += ContentFrame_Navigated;

        // Page par défaut
        ContentFrame.Navigate(typeof(HomePage));
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            VM.SetHeader("Paramètres");
            ContentFrame.Navigate(typeof(SettingsPage));
            return;
        }

        var key = (args.SelectedItemContainer?.Tag as string) ?? "";

        switch (key)
        {
            case "Home":
                VM.SetHeader("Accueil");
                ContentFrame.Navigate(typeof(HomePage));
                break;

            case "Items":
                VM.SetHeader("Items");
                ContentFrame.Navigate(typeof(ItemsPage));
                break;
        }
    }

    private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
    {
        // Attache/détache selon la page affichée
        if (e.Content is ItemsPage itemsPage && itemsPage.DataContext is ItemsViewModel itemsVm)
        {
            VM.AttachItems(itemsVm);
        }
        else
        {
            VM.DetachItems();
        }
    }
}
