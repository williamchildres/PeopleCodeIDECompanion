using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PeopleCodeIDECompanion.Models;

namespace PeopleCodeIDECompanion.Services;

public sealed class SavedOracleConnectionStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;

    public SavedOracleConnectionStore()
    {
        string appDataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PeopleCodeIDECompanion");
        _filePath = Path.Combine(appDataDirectory, "saved-connections.json");
    }

    public async Task<IReadOnlyList<SavedOracleConnectionProfile>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return Array.Empty<SavedOracleConnectionProfile>();
        }

        await using FileStream stream = File.OpenRead(_filePath);
        List<SavedOracleConnectionProfile>? profiles = await JsonSerializer.DeserializeAsync<List<SavedOracleConnectionProfile>>(
            stream,
            SerializerOptions,
            cancellationToken);

        return profiles is null
            ? Array.Empty<SavedOracleConnectionProfile>()
            : profiles
                .OrderBy(profile => profile.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
    }

    public async Task SaveAsync(SavedOracleConnectionProfile profile, CancellationToken cancellationToken = default)
    {
        List<SavedOracleConnectionProfile> profiles = (await LoadAsync(cancellationToken)).ToList();
        int existingIndex = profiles.FindIndex(existing =>
            existing.DisplayName.Equals(profile.DisplayName, StringComparison.OrdinalIgnoreCase));

        if (existingIndex >= 0)
        {
            profiles[existingIndex] = profile;
        }
        else
        {
            profiles.Add(profile);
        }

        await WriteAsync(profiles, cancellationToken);
    }

    public async Task DeleteAsync(string displayName, CancellationToken cancellationToken = default)
    {
        List<SavedOracleConnectionProfile> profiles = (await LoadAsync(cancellationToken))
            .Where(profile => !profile.DisplayName.Equals(displayName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        await WriteAsync(profiles, cancellationToken);
    }

    private async Task WriteAsync(
        IReadOnlyCollection<SavedOracleConnectionProfile> profiles,
        CancellationToken cancellationToken)
    {
        string? directory = Path.GetDirectoryName(_filePath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using FileStream stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, profiles, SerializerOptions, cancellationToken);
    }
}
