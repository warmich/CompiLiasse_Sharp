using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;

namespace CompiLiasse_Sharp.ViewModels;

public partial class ShellViewModel : ObservableObject
{
    private ItemsViewModel? _itemsVm;

    private string _headerTitle = "Accueil";
    public string HeaderTitle
    {
        get => _headerTitle;
        set => SetProperty(ref _headerTitle, value);
    }

    public void AttachItems(ItemsViewModel vm) => _itemsVm = vm;
    public void DetachItems() => _itemsVm = null;

    [RelayCommand]
    private async Task New()
    {
        if (_itemsVm is null)
        {
            HeaderTitle = "Nouveau (pas sur Items)";
            return;
        }

        await _itemsVm.AddItemAsync();
        HeaderTitle = "Items";
    }

    [RelayCommand]
    private async Task Refresh()
    {
        if (_itemsVm is null)
        {
            HeaderTitle = "Rafraîchir (pas sur Items)";
            return;
        }

        await _itemsVm.RefreshItemsAsync();
        HeaderTitle = "Items";
    }

    public void SetHeader(string title) => HeaderTitle = title;
}
