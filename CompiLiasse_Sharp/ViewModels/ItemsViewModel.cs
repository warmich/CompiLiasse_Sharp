using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CompiLiasse_Sharp.Models;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace CompiLiasse_Sharp.ViewModels;

public partial class ItemsViewModel : ObservableObject
{
    public ObservableCollection<Item> Items { get; } = new();

    private Item? _selectedItem;
    public Item? SelectedItem
    {
        get => _selectedItem;
        set => SetProperty(ref _selectedItem, value);
    }

    private string _newItemName = "";
    public string NewItemName
    {
        get => _newItemName;
        set => SetProperty(ref _newItemName, value);
    }

    private List<Item> _allItems = new();

    private string _searchText = "";
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetProperty(ref _searchText, value))
                return;

            ApplyFilter();
        }
    }

    private bool _isEmpty;
    public bool IsEmpty
    {
        get => _isEmpty;
        private set => SetProperty(ref _isEmpty, value);
    }

    private Services.DbService Db => ((App)Application.Current).Db;

    public ItemsViewModel()
    {
        // Ne rien lancer ici (évite les erreurs silencieuses au démarrage)

        // Avant problème de rétention => Chargement initial “fire and forget” (simple)
        // _ = RefreshItemsAsync();
    }

    // Avant problème de rétention => puis supprime complètement ces deux méthodes
    //public void AddItem()
    //{
    //    _ = AddItemAsync();
    //}

    //public void RefreshItems()
    //{
    //    _ = RefreshItemsAsync();
    //}

    public async Task AddItemAsync()
    {
        var name = NewItemName?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            name = $"Item {DateTime.Now:HH:mm:ss}";

        await Db.InsertItemAsync(name);

        NewItemName = "";
        await RefreshItemsAsync();
    }

    public async Task RefreshItemsAsync()
    {
        _allItems = await Db.GetItemsAsync();
        ApplyFilter();

        if (SelectedItem != null && !Items.Any(i => i.Id == SelectedItem.Id))
            SelectedItem = null;
    }

    private void ApplyFilter()
    {
        var q = (SearchText ?? "").Trim().ToLowerInvariant();

        var filtered = string.IsNullOrWhiteSpace(q)
            ? _allItems
            : _allItems.Where(i =>
                  i.Id.ToString().Contains(q) ||
                  i.Name.ToLowerInvariant().Contains(q) ||
                  i.CreatedAtLocalText.ToLowerInvariant().Contains(q))
              .ToList();

        Items.Clear();
        foreach (var item in filtered)
            Items.Add(item);

        IsEmpty = Items.Count == 0;
    }

    public async Task DeleteSelectedAsync()
    {
        if (SelectedItem is null) return;

        await Db.DeleteItemAsync(SelectedItem.Id);
        SelectedItem = null;
        await RefreshItemsAsync();
    }
}
