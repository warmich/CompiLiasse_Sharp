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
    private bool _isApplyingSelection;     // évite de créer un draft quand on remplit les champs après sélection
    private bool _isEnsuringDraft;         // évite re-entrance

    private SettingsProfile? _selectedProfile;
    public SettingsProfile? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (!SetProperty(ref _selectedProfile, value))
                return;

            _isApplyingSelection = true;
            try
            {
                ExcelPathText = _selectedProfile?.ExcelPath ?? "";
                FolderPathText = _selectedProfile?.FolderPath ?? "";

                ExcelPathError = null;
                FolderPathError = null;
            }
            finally
            {
                _isApplyingSelection = false;
            }

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
            //EnsureDraftIfEditing();
            if (!_isApplyingSelection)
                _ = EnsureDraftIfEditingAsync();
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

            // Règle brouillon : au premier changement, basculer sur CONFIG_NON_SAUVEGARDEE
            //EnsureDraftIfEditing();
            if (!_isApplyingSelection)
                _ = EnsureDraftIfEditingAsync();
        }
    }

    private async Task EnsureDraftIfEditingAsync()
    {
        if (_isEnsuringDraft) return;
        if (SelectedProfile is null) return;

        // Si déjà sur draft -> rien à faire
        if (SelectedProfile.IsDraft) return;

        // Si on est en train de charger/sélectionner -> ne pas créer
        if (_isApplyingSelection) return;

        _isEnsuringDraft = true;
        try
        {
            // Crée / met à jour le draft dans la DB avec les valeurs en cours
            var draftId = await Db.UpsertDraftAsync(
                string.IsNullOrWhiteSpace(ExcelPathText) ? null : ExcelPathText.Trim(),
                string.IsNullOrWhiteSpace(FolderPathText) ? null : FolderPathText.Trim()
            );

            // Récupère l’objet draft depuis la liste ou recharge le draft
            var draft = Profiles.FirstOrDefault(p => p.IsDraft);
            if (draft is null)
            {
                var dbDraft = await Db.GetDraftAsync();
                if (dbDraft is null) return;

                // s’assure qu’il a l’Id réel
                draft = dbDraft;
                Profiles.Insert(1, draft); // juste après DEFAUT
            }

            draft.Id = draftId;
            draft.Name = "CONFIG_NON_SAUVEGARDEE";
            draft.ExcelPath = string.IsNullOrWhiteSpace(ExcelPathText) ? null : ExcelPathText.Trim();
            draft.FolderPath = string.IsNullOrWhiteSpace(FolderPathText) ? null : FolderPathText.Trim();
            draft.IsActive = true;
            draft.IsDraft = true;

            // Switch sur draft (et persiste LastProfileKey via ton mécanisme point 3)
            SelectedProfile = draft;
        }
        catch
        {
            // simple : ignore pour l’instant
        }
        finally
        {
            _isEnsuringDraft = false;
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
    public async Task OnExcelLostFocusAsync()
    {
        if (SelectedProfile is null) return;
        if (ExcelPathError != null) return;

        var value = string.IsNullOrWhiteSpace(ExcelPathText) ? null : ExcelPathText.Trim();

        // On écrit en DB uniquement si profil DB (draft ou normal)
        if (SelectedProfile.Id > 0)
            await Db.UpdateProfileExcelPathAsync(SelectedProfile.Id, value);

        // cohérence en mémoire
        SelectedProfile.ExcelPath = value;
    }

    public async Task OnFolderLostFocusAsync()
    {
        if (SelectedProfile is null) return;
        if (FolderPathError != null) return;

        var value = string.IsNullOrWhiteSpace(FolderPathText) ? null : FolderPathText.Trim();

        if (SelectedProfile.Id > 0)
            await Db.UpdateProfileFolderPathAsync(SelectedProfile.Id, value);

        SelectedProfile.FolderPath = value;
    }

    // Dummy “Enregistrer sous…” : transforme le draft en profil normal
    public async Task<(bool Success, string? Error)> SaveAsAsync(string name)
    {
        string? error = null;
        name = (name ?? "").Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            error = "Nom invalide.";
            return (false, error);
        }

        if (string.Equals(name, "DEFAUT", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "CONFIG_NON_SAUVEGARDEE", StringComparison.OrdinalIgnoreCase))
        {
            error = "Nom réservé.";
            return (false, error);
        }

        // Unicité sur la liste active (hors draft)
        if (Profiles.Any(p => !p.IsDraft && string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            error = "Ce nom existe déjà.";
            return (false, error);
        }

        var excel = string.IsNullOrWhiteSpace(ExcelPathText) ? null : ExcelPathText.Trim();
        var folder = string.IsNullOrWhiteSpace(FolderPathText) ? null : FolderPathText.Trim();

        // Crée le nouveau profil
        var newId = await Db.InsertProfileAsync(name, excel, folder, isDraft: false);

        // Désactive le draft si présent
        await Db.ClearDraftAsync();

        // Recharge la liste (et restaure la sélection sur le nouveau)
        await LoadAsync();

        var created = Profiles.FirstOrDefault(p => p.Id == newId);
        SelectedProfile = created ?? DefaultProfile;

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> AddProfileAsync(string name)
    {
        name = (name ?? "").Trim();

        if (string.IsNullOrWhiteSpace(name))
            return (false, "Nom invalide.");

        if (string.Equals(name, "DEFAUT", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "CONFIG_NON_SAUVEGARDEE", StringComparison.OrdinalIgnoreCase))
            return (false, "Nom réservé.");

        // Unicité sur les profils actifs (hors draft)
        if (Profiles.Any(p => !p.IsDraft && string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
            return (false, "Ce nom existe déjà.");

        // Nouveau profil vide (Excel/Folder vides), non draft
        var newId = await Db.InsertProfileAsync(name, excelPath: null, folderPath: null, isDraft: false);

        // Recharge et sélectionne le nouveau profil
        await LoadAsync();
        var created = Profiles.FirstOrDefault(p => p.Id == newId);
        SelectedProfile = created ?? DefaultProfile;

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> DeactivateSelectedAsync()
    {
        if (SelectedProfile is null)
            return (false, "Aucune configuration sélectionnée.");

        // Interdits
        if (SelectedProfile.Id == 0 || string.Equals(SelectedProfile.Name, "DEFAUT", StringComparison.OrdinalIgnoreCase))
            return (false, "La configuration DEFAUT ne peut pas être désactivée.");

        // Si c'est le draft, on le “clear” (soft delete aussi)
        if (SelectedProfile.IsDraft)
        {
            await Db.ClearDraftAsync();
            await LoadAsync();
            SelectedProfile = DefaultProfile;
            return (true, null);
        }

        // Profil normal : soft delete
        await Db.DeactivateProfileAsync(SelectedProfile.Id);

        await LoadAsync();
        SelectedProfile = DefaultProfile;

        return (true, null);
    }

}
