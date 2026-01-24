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
    private const string DraftName = "CONFIG_NON_SAUVEGARDEE";

    public DbService()
    {
        _dbPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "app.db");
    }

    private string ConnectionString => $"Data Source={_dbPath}";

    public async Task InitializeAsync()
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();

        // --- Items (déjà chez toi) ---
        {
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

            // Migration simple: FolderPath
            var alter = connection.CreateCommand();
            alter.CommandText = "ALTER TABLE Items ADD COLUMN FolderPath TEXT NULL;";
            try { await alter.ExecuteNonQueryAsync(); } catch { }
        }

        // --- SettingsProfiles ---
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText =
            """
        CREATE TABLE IF NOT EXISTS SettingsProfiles (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL,
            ExcelPath TEXT NULL,
            FolderPath TEXT NULL,
            IsActive INTEGER NOT NULL DEFAULT 1,
            IsDraft INTEGER NOT NULL DEFAULT 0,
            CreatedAtUtc TEXT NOT NULL,
            UpdatedAtUtc TEXT NOT NULL
        );
        """;
            await cmd.ExecuteNonQueryAsync();

            // Index utiles
            var idx = connection.CreateCommand();
            idx.CommandText =
            """
        CREATE INDEX IF NOT EXISTS IX_SettingsProfiles_IsActive ON SettingsProfiles(IsActive);
        CREATE INDEX IF NOT EXISTS IX_SettingsProfiles_IsDraft ON SettingsProfiles(IsDraft);
        """;
            await idx.ExecuteNonQueryAsync();
        }

        // --- AppState (clé/valeur) ---
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText =
            """
        CREATE TABLE IF NOT EXISTS AppState (
            Key TEXT PRIMARY KEY,
            Value TEXT NOT NULL
        );
        """;
            await cmd.ExecuteNonQueryAsync();
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

    public async Task<List<SettingsProfile>> GetActiveProfilesAsync()
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();

        var cmd = connection.CreateCommand();
        cmd.CommandText =
        """
        SELECT Id, Name, ExcelPath, FolderPath, IsActive, IsDraft, CreatedAtUtc, UpdatedAtUtc
        FROM SettingsProfiles
        WHERE IsActive = 1
        ORDER BY IsDraft DESC, Name COLLATE NOCASE ASC;
        """;

        var list = new List<SettingsProfile>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new SettingsProfile
            {
                Id = reader.GetInt64(0),
                Name = reader.GetString(1),
                ExcelPath = reader.IsDBNull(2) ? null : reader.GetString(2),
                FolderPath = reader.IsDBNull(3) ? null : reader.GetString(3),
                IsActive = reader.GetInt64(4) == 1,
                IsDraft = reader.GetInt64(5) == 1
            });
        }

        return list;
    }

    public async Task<long> InsertProfileAsync(string name, string? excelPath, string? folderPath, bool isDraft)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();

        var now = DateTime.UtcNow.ToString("O");

        var cmd = connection.CreateCommand();
        cmd.CommandText =
        """
        INSERT INTO SettingsProfiles (Name, ExcelPath, FolderPath, IsActive, IsDraft, CreatedAtUtc, UpdatedAtUtc)
        VALUES ($name, $excel, $folder, 1, $isDraft, $created, $updated);
        SELECT last_insert_rowid();
        """;
        cmd.Parameters.AddWithValue("$name", name);
        cmd.Parameters.AddWithValue("$excel", (object?)excelPath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$folder", (object?)folderPath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$isDraft", isDraft ? 1 : 0);
        cmd.Parameters.AddWithValue("$created", now);
        cmd.Parameters.AddWithValue("$updated", now);

        var result = await cmd.ExecuteScalarAsync();
        return (long)(result ?? 0L);
    }

    public async Task UpdateProfileExcelPathAsync(long id, string? excelPath)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();

        var now = DateTime.UtcNow.ToString("O");

        var cmd = connection.CreateCommand();
        cmd.CommandText =
        """
        UPDATE SettingsProfiles
        SET ExcelPath = $excel, UpdatedAtUtc = $now
        WHERE Id = $id;
        """;
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$excel", (object?)excelPath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$now", now);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdateProfileFolderPathAsync(long id, string? folderPath)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();

        var now = DateTime.UtcNow.ToString("O");

        var cmd = connection.CreateCommand();
        cmd.CommandText =
        """
        UPDATE SettingsProfiles
        SET FolderPath = $folder, UpdatedAtUtc = $now
        WHERE Id = $id;
        """;
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$folder", (object?)folderPath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$now", now);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdateProfileNameAsync(long id, string name)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();

        var now = DateTime.UtcNow.ToString("O");

        var cmd = connection.CreateCommand();
        cmd.CommandText =
        """
        UPDATE SettingsProfiles
        SET Name = $name, UpdatedAtUtc = $now
        WHERE Id = $id;
        """;
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$name", name);
        cmd.Parameters.AddWithValue("$now", now);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeactivateProfileAsync(long id)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();

        var now = DateTime.UtcNow.ToString("O");

        var cmd = connection.CreateCommand();
        cmd.CommandText =
        """
        UPDATE SettingsProfiles
        SET IsActive = 0, UpdatedAtUtc = $now
        WHERE Id = $id;
        """;
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$now", now);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<SettingsProfile?> GetDraftAsync()
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();

        var cmd = connection.CreateCommand();
        cmd.CommandText =
        """
        SELECT Id, Name, ExcelPath, FolderPath, IsActive, IsDraft
        FROM SettingsProfiles
        WHERE IsActive = 1 AND IsDraft = 1
        LIMIT 1;
        """;

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        return new SettingsProfile
        {
            Id = reader.GetInt64(0),
            Name = reader.GetString(1),
            ExcelPath = reader.IsDBNull(2) ? null : reader.GetString(2),
            FolderPath = reader.IsDBNull(3) ? null : reader.GetString(3),
            IsActive = reader.GetInt64(4) == 1,
            IsDraft = reader.GetInt64(5) == 1
        };
    }

    public async Task<long> UpsertDraftAsync(string? excelPath, string? folderPath)
    {
        var existing = await GetDraftAsync();
        if (existing is null)
        {
            return await InsertProfileAsync(DraftName, excelPath, folderPath, isDraft: true);
        }

        await UpdateProfileExcelPathAsync(existing.Id, excelPath);
        await UpdateProfileFolderPathAsync(existing.Id, folderPath);
        return existing.Id;
    }

    public async Task ClearDraftAsync()
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();

        var now = DateTime.UtcNow.ToString("O");

        var cmd = connection.CreateCommand();
        cmd.CommandText =
        """
        UPDATE SettingsProfiles
        SET IsActive = 0, UpdatedAtUtc = $now
        WHERE IsDraft = 1 AND IsActive = 1;
        """;
        cmd.Parameters.AddWithValue("$now", now);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<string?> GetAppStateAsync(string key)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();

        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Value FROM AppState WHERE Key = $k;";
        cmd.Parameters.AddWithValue("$k", key);

        var result = await cmd.ExecuteScalarAsync();
        return result as string;
    }

    public async Task SetAppStateAsync(string key, string value)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();

        var cmd = connection.CreateCommand();
        cmd.CommandText =
        """
        INSERT INTO AppState(Key, Value)
        VALUES($k, $v)
        ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;
        """;
        cmd.Parameters.AddWithValue("$k", key);
        cmd.Parameters.AddWithValue("$v", value);

        await cmd.ExecuteNonQueryAsync();
    }

}
