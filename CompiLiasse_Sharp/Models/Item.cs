using System;

namespace CompiLiasse_Sharp.Models;

public sealed class Item
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }

    // Pratique pour l’affichage
    public string CreatedAtLocalText => CreatedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

    public string? FolderPath { get; set; }

    public string FolderDisplay => string.IsNullOrWhiteSpace(FolderPath) ? "(aucun dossier)" : FolderPath!;

}
