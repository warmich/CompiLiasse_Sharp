using CommunityToolkit.Mvvm.ComponentModel;
using CompiLiasse_Sharp.Models;
using Microsoft.UI.Xaml;
using Windows.ApplicationModel;
using System;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace CompiLiasse_Sharp.ViewModels;

public sealed class SettingsProfilesViewModel : ObservableObject
{
    private Services.DbService Db => ((App)Application.Current).Db;

    private const string LastProfileKeyName = "LastProfileKey";
    private const string LastProfileDefault = "DEFAULT";
    private const string LastProfileDraft = "DRAFT";

    public ObservableCollection<SettingsProfile> Profiles { get; } = new();

    private bool _isLoading;
    private bool _suppressPersistSelection;

    private SettingsProfile? _selectedProfile;
    public SettingsProfile? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (!SetProperty(ref _selectedProfile, value))
                return;

            ExcelPathText = _selectedProfile?.ExcelPath ?? "";
            FolderPathText = _selectedProfile?.FolderPath ?? "";

            ExcelPathError = null;
            FolderPathError = null;

            // Persiste la sélection (sans bloquer l’UI)
            if (!_suppressPersistSelection)
                _ = PersistLastSelectionAsync(_selectedProfile);
        }
    }

    private async Task PersistLastSelectionAsync(SettingsProfile? profile)
    {
        try
        {
            if (profile is null)
                return;

            if (profile.Id == 0 || string.Equals(profile.Name, "DEFAUT", StringComparison.OrdinalIgnoreCase))
            {
                await Db.SetAppStateAsync(LastProfileKeyName, LastProfileDefault);
                return;
            }

            if (profile.IsDraft)
            {
                await Db.SetAppStateAsync(LastProfileKeyName, LastProfileDraft);
                return;
            }

            await Db.SetAppStateAsync(LastProfileKeyName, profile.Id.ToString());
        }
        catch
        {
            // ultra simple : on ignore pour l'instant (log possible)
        }
    }

    // Champs bindés aux TextBox
    private string _excelPathText = "";
    public string ExcelPathText
    {
        get => _excelPathText;
        set
        {
            if (!SetProperty(ref _excelPathText, value))
                return;

            // Validation immédiate (UI)
            ExcelPathError = ValidateExcelPath(value);

            // Règle brouillon : au premier changement, basculer sur CONFIG_NON_SAUVEGARDEE
            EnsureDraftIfEditing();
        }
    }

    private string _folderPathText = "";
    public string FolderPathText
    {
        get => _folderPathText;
        set
        {
            if (!SetProperty(ref _folderPathText, value))
                return;

            FolderPathError = ValidateFolderPath(value);
            EnsureDraftIfEditing();
        }
    }

    // Messages d'erreur
    private string? _excelPathError;
    public string? ExcelPathError
    {
        get => _excelPathError;
        set => SetProperty(ref _excelPathError, value);
    }

    private string? _folderPathError;
    public string? FolderPathError
    {
        get => _folderPathError;
        set => SetProperty(ref _folderPathError, value);
    }

    // Profil par défaut persistant
    public SettingsProfile DefaultProfile { get; } = new()
    {
        Id = 0,
        Name = "DEFAUT",
        ExcelPath = null,
        FolderPath = null,
        IsActive = true,
        IsDraft = false
    };

    private SettingsProfile? _draftProfile; // CONFIG_NON_SAUVEGARDEE
    public SettingsProfile? DraftProfile => _draftProfile;

    private readonly MainWindow _window;

    public SettingsProfilesViewModel(MainWindow window)
    {
        _window = window;

        ThemeIndex = 0; // système par défaut
        AppVersion = GetAppVersion();
        PackageName = GetPackageName();
    }

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

            // Applique le thème sur le root de la window (simple)
            if (_window.Content is FrameworkElement root)
                root.RequestedTheme = theme;
        }
    }

    private string _appVersion = "";
    public string AppVersion
    {
        get => _appVersion;
        private set => SetProperty(ref _appVersion, value);
    }

    private string _packageName = "";
    public string PackageName
    {
        get => _packageName;
        private set => SetProperty(ref _packageName, value);
    }

    private static string GetAppVersion()
    {
        try
        {
            var v = Package.Current.Id.Version;
            return $"{v.Major}.{v.Minor}.{v.Build}.{v.Revision}";
        }
        catch
        {
            return "Non packagé";
        }
    }

    private static string GetPackageName()
    {
        try { return Package.Current.DisplayName; }
        catch { return "CompiLiasse"; }
    }

    public async Task LoadAsync()
    {
        if (_isLoading) return;
        _isLoading = true;

        try
        {
            _suppressPersistSelection = true;

            Profiles.Clear();
            Profiles.Add(DefaultProfile);

            var dbProfiles = await Db.GetActiveProfilesAsync();

            // On ajoute tout sauf DEFAUT (qui n’existe pas en DB) :
            foreach (var p in dbProfiles)
            {
                // Sécurité : évite un doublon si un profil DB s'appelle "DEFAUT"
                if (string.Equals(p.Name, "DEFAUT", StringComparison.OrdinalIgnoreCase))
                    continue;

                Profiles.Add(p);
            }

            // Restaurer dernière sélection
            var last = await Db.GetAppStateAsync(LastProfileKeyName);

            if (string.IsNullOrWhiteSpace(last) || last == LastProfileDefault)
            {
                SelectedProfile = DefaultProfile;
            }
            else if (last == LastProfileDraft)
            {
                var draft = Profiles.FirstOrDefault(p => p.IsDraft);
                SelectedProfile = draft ?? DefaultProfile;
            }
            else if (long.TryParse(last, out var id))
            {
                var found = Profiles.FirstOrDefault(p => p.Id == id);
                SelectedProfile = found ?? DefaultProfile;
            }
            else
            {
                SelectedProfile = DefaultProfile;
            }
        }
        finally
        {
            _suppressPersistSelection = false;
            _isLoading = false;
        }
    }

    //public void LoadDummy()
    //{
    //    Profiles.Clear();

    //    // Toujours en tête
    //    Profiles.Add(DefaultProfile);

    //    Profiles.Add(new SettingsProfile
    //    {
    //        Id = 1,
    //        Name = "CONFIG1",
    //        ExcelPath = @"C:\Dossiers\Compta\lias se.xlsx",
    //        FolderPath = @"C:\Dossiers\Compta",
    //        IsActive = true
    //    });

    //    Profiles.Add(new SettingsProfile
    //    {
    //        Id = 2,
    //        Name = "CONFIG2",
    //        ExcelPath = @"D:\Data\Fichier.xlsx",
    //        FolderPath = @"D:\Data",
    //        IsActive = true
    //    });

    //    SelectedProfile = DefaultProfile;
    //}

    private void EnsureDraftIfEditing()
    {
        if (SelectedProfile is null)
            return;

        // Si déjà draft => rien
        if (SelectedProfile.IsDraft)
            return;

        // Si l'utilisateur modifie, on bascule en draft (sauf si on est déjà sur draft)
        // Ici on déclenche à chaque set, mais on ne crée le draft qu'une fois.
        // Condition : on a un SelectedProfile "normal" ou DEFAUT.
        if (_draftProfile is null)
        {
            _draftProfile = new SettingsProfile
            {
                Id = -1, // dummy (en DB on aura un vrai Id)
                Name = "CONFIG_NON_SAUVEGARDEE",
                ExcelPath = SelectedProfile.ExcelPath,
                FolderPath = SelectedProfile.FolderPath,
                IsActive = true,
                IsDraft = true
            };
        }

        // Si on a changé les champs alors qu'on est sur un profil normal => switch vers draft
        if (SelectedProfile.Id != _draftProfile.Id && !ReferenceEquals(SelectedProfile, _draftProfile))
        {
            // On garde les valeurs en cours (celles de l'UI)
            _draftProfile.ExcelPath = ExcelPathText;
            _draftProfile.FolderPath = FolderPathText;

            // Ajouter au combo si pas présent
            if (!Profiles.Any(p => p.IsDraft))
                Profiles.Insert(1, _draftProfile); // juste après DEFAUT

            SelectedProfile = _draftProfile;
        }
        else
        {
            // On est déjà sur draft, on met à jour l'objet draft
            _draftProfile.ExcelPath = ExcelPathText;
            _draftProfile.FolderPath = FolderPathText;
        }
    }

    public string? ValidateExcelPath(string? text)
    {
        var path = (text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(path))
            return null; // vide autorisé pour l'étape 1

        // Vérifie existence fichier
        if (!File.Exists(path))
            return "Fichier introuvable.";

        // Vérifie extension (simple)
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext != ".xlsx" && ext != ".xlsm" && ext != ".xls")
            return "Extension non supportée (xlsx/xlsm/xls).";

        return null;
    }

    public string? ValidateFolderPath(string? text)
    {
        var path = (text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(path))
            return null;

        if (!Directory.Exists(path))
            return "Dossier introuvable.";

        return null;
    }

    // Appelé au LostFocus (étape 1 : on simule juste le “save” en mémoire)
    public void OnExcelLostFocus()
    {
        if (SelectedProfile is null) return;
        if (ExcelPathError != null) return;

        SelectedProfile.ExcelPath = string.IsNullOrWhiteSpace(ExcelPathText) ? null : ExcelPathText.Trim();
    }

    public void OnFolderLostFocus()
    {
        if (SelectedProfile is null) return;
        if (FolderPathError != null) return;

        SelectedProfile.FolderPath = string.IsNullOrWhiteSpace(FolderPathText) ? null : FolderPathText.Trim();
    }

    // Dummy “Enregistrer sous…” : transforme le draft en profil normal
    public bool SaveAs(string name, out string? error)
    {
        error = null;
        name = (name ?? "").Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            error = "Nom invalide.";
            return false;
        }

        if (string.Equals(name, "DEFAUT", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "CONFIG_NON_SAUVEGARDEE", StringComparison.OrdinalIgnoreCase))
        {
            error = "Nom réservé.";
            return false;
        }

        if (Profiles.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            error = "Ce nom existe déjà.";
            return false;
        }

        var newId = Profiles.Max(p => p.Id) + 1;

        var pNew = new SettingsProfile
        {
            Id = newId,
            Name = name,
            ExcelPath = string.IsNullOrWhiteSpace(ExcelPathText) ? null : ExcelPathText.Trim(),
            FolderPath = string.IsNullOrWhiteSpace(FolderPathText) ? null : FolderPathText.Trim(),
            IsActive = true,
            IsDraft = false
        };

        // retirer draft si présent
        var draftInList = Profiles.FirstOrDefault(p => p.IsDraft);
        if (draftInList != null)
            Profiles.Remove(draftInList);

        _draftProfile = null;

        Profiles.Add(pNew);
        SelectedProfile = pNew;

        return true;
    }
}
