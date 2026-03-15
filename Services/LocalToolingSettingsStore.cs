using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PeopleCodeIDECompanion.Models;
using Windows.Storage;

namespace PeopleCodeIDECompanion.Services;

public sealed class LocalToolingSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;

    public LocalToolingSettingsStore()
    {
        string appDataDirectory = ResolveAppDataDirectory();
        _filePath = Path.Combine(appDataDirectory, "local-tooling.json");
    }

    public async Task<LocalToolingSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return new LocalToolingSettings();
        }

        await using FileStream stream = File.OpenRead(_filePath);
        LocalToolingSettings? settings = await JsonSerializer.DeserializeAsync<LocalToolingSettings>(
            stream,
            SerializerOptions,
            cancellationToken);

        return Normalize(settings);
    }

    public async Task SaveAsync(LocalToolingSettings settings, CancellationToken cancellationToken = default)
    {
        string? directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using FileStream stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, Normalize(settings), SerializerOptions, cancellationToken);
    }

    private static LocalToolingSettings Normalize(LocalToolingSettings? settings)
    {
        LocalToolingSettings source = settings ?? new LocalToolingSettings();
        return new LocalToolingSettings
        {
            PsidePath = source.PsidePath?.Trim() ?? string.Empty
        };
    }

    private static string ResolveAppDataDirectory()
    {
        try
        {
            return ApplicationData.Current.LocalFolder.Path;
        }
        catch
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PeopleCodeIDECompanion");
        }
    }
}
