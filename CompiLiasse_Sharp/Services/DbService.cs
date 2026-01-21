using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using CompiLiasse_Sharp.Models;
using System.Globalization;

namespace CompiLiasse_Sharp.Services;

public sealed class DbService
{
    private readonly string _dbPath;

    public DbService()
    {
        _dbPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "app.db");
    }

    private string ConnectionString => $"Data Source={_dbPath}";

    public async Task InitializeAsync()
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();

        // 1) Créer la table (tu avais le SQL mais tu ne l’exécutais pas)
        var cmd = connection.CreateCommand();
        cmd.CommandText =
        """
        CREATE TABLE IF NOT EXISTS Items (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL,
            CreatedAtUtc TEXT NOT NULL,
            FolderPath TEXT NULL
        );
        """;
        await cmd.ExecuteNonQueryAsync();

        // 2) Migration simple : ajouter la colonne FolderPath si elle n’existe pas
        var alter = connection.CreateCommand();
        alter.CommandText = "ALTER TABLE Items ADD COLUMN FolderPath TEXT NULL;";
        try
        {
            await alter.ExecuteNonQueryAsync();
        }
        catch
        {
            // Si la colonne existe déjà, SQLite renverra une erreur : on ignore.
        }
    }

    public async Task<long> InsertItemAsync(string name)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();

        var createdAtUtc = DateTime.UtcNow.ToString("O");

        var cmd = connection.CreateCommand();
        cmd.CommandText =
        """
        INSERT INTO Items (Name, CreatedAtUtc)
        VALUES ($name, $createdAtUtc);
        SELECT last_insert_rowid();
        """;
        cmd.Parameters.AddWithValue("$name", name);
        cmd.Parameters.AddWithValue("$createdAtUtc", createdAtUtc);

        var result = await cmd.ExecuteScalarAsync();
        return (long)(result ?? 0L);
    }

    public async Task<List<Item>> GetItemsAsync()
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();

        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, CreatedAtUtc, FolderPath FROM Items ORDER BY Id DESC;\r\n";

        var list = new List<Item>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var id = reader.GetInt64(0);
            var name = reader.GetString(1);
            var createdAt = DateTime.Parse(
                reader.GetString(2),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind);
            var folderPath = reader.IsDBNull(3) ? null : reader.GetString(3);

            list.Add(new Item
            {
                Id = id,
                Name = name,
                CreatedAtUtc = createdAt,
                FolderPath = folderPath
            });
        }

        return list;
    }

    public async Task DeleteItemAsync(long id)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();

        var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Items WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$id", id);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdateItemFolderAsync(long id, string? folderPath)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();

        var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE Items SET FolderPath = $path WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$path", (object?)folderPath ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync();
    }
}
