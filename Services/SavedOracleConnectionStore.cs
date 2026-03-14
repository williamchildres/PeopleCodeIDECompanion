using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PeopleCodeIDECompanion.Models;
using Windows.Storage;

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
        string appDataDirectory = ResolveAppDataDirectory();
        _filePath = Path.Combine(appDataDirectory, "saved-connections.json");
    }

    public async Task<IReadOnlyList<SavedOracleConnectionProfile>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return Array.Empty<SavedOracleConnectionProfile>();
        }

        List<SavedOracleConnectionProfile>? profiles;
        await using (FileStream stream = File.OpenRead(_filePath))
        {
            profiles = await JsonSerializer.DeserializeAsync<List<SavedOracleConnectionProfile>>(
                stream,
                SerializerOptions,
                cancellationToken);
        }

        List<SavedOracleConnectionProfile> normalizedProfiles = profiles is null
            ? []
            : profiles
                .Select(NormalizeProfile)
                .OrderBy(profile => profile.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();

        if (profiles is not null && ProfilesNeedNormalization(profiles, normalizedProfiles))
        {
            await WriteAsync(normalizedProfiles, cancellationToken);
        }

        return normalizedProfiles;
    }

    public async Task SaveAsync(SavedOracleConnectionProfile profile, CancellationToken cancellationToken = default)
    {
        List<SavedOracleConnectionProfile> profiles = (await LoadAsync(cancellationToken)).ToList();
        SavedOracleConnectionProfile normalizedProfile = NormalizeProfile(profile);
        int existingIndex = profiles.FindIndex(existing => ProfileMatches(existing, normalizedProfile));

        if (existingIndex >= 0)
        {
            profiles[existingIndex] = normalizedProfile;
        }
        else
        {
            profiles.Add(normalizedProfile);
        }

        await WriteAsync(profiles, cancellationToken);
    }

    public async Task DeleteAsync(string profileId, CancellationToken cancellationToken = default)
    {
        List<SavedOracleConnectionProfile> profiles = (await LoadAsync(cancellationToken))
            .Where(profile => !profile.ProfileId.Equals(profileId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        await WriteAsync(profiles, cancellationToken);
    }

    public async Task UpdateLastConnectedAsync(
        string profileId,
        DateTimeOffset lastConnectedAt,
        CancellationToken cancellationToken = default)
    {
        List<SavedOracleConnectionProfile> profiles = (await LoadAsync(cancellationToken)).ToList();
        SavedOracleConnectionProfile? existingProfile = profiles.FirstOrDefault(profile =>
            profile.ProfileId.Equals(profileId, StringComparison.OrdinalIgnoreCase));

        if (existingProfile is null)
        {
            return;
        }

        existingProfile.LastConnectedAt = lastConnectedAt;
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

    private static SavedOracleConnectionProfile NormalizeProfile(SavedOracleConnectionProfile profile)
    {
        string profileId = string.IsNullOrWhiteSpace(profile.ProfileId)
            ? Guid.NewGuid().ToString("N")
            : profile.ProfileId.Trim();

        return new SavedOracleConnectionProfile
        {
            ProfileId = profileId,
            DisplayName = profile.DisplayName.Trim(),
            Host = profile.Host.Trim(),
            Port = string.IsNullOrWhiteSpace(profile.Port) ? "1521" : profile.Port.Trim(),
            ServiceName = profile.ServiceName.Trim(),
            Username = profile.Username.Trim(),
            AutoLoginEnabled = profile.AutoLoginEnabled,
            CredentialTargetId = string.IsNullOrWhiteSpace(profile.CredentialTargetId)
                ? CreateCredentialTargetId(profileId)
                : profile.CredentialTargetId.Trim(),
            LastConnectedAt = profile.LastConnectedAt,
            OverviewSettings = PeopleCodeOverviewProfileSettings.Normalize(profile.OverviewSettings)
        };
    }

    private static bool ProfileMatches(SavedOracleConnectionProfile existing, SavedOracleConnectionProfile updated)
    {
        if (!string.IsNullOrWhiteSpace(updated.ProfileId))
        {
            return existing.ProfileId.Equals(updated.ProfileId, StringComparison.OrdinalIgnoreCase);
        }

        return existing.DisplayName.Equals(updated.DisplayName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ProfilesNeedNormalization(
        IReadOnlyList<SavedOracleConnectionProfile> originalProfiles,
        IReadOnlyList<SavedOracleConnectionProfile> normalizedProfiles)
    {
        if (originalProfiles.Count != normalizedProfiles.Count)
        {
            return true;
        }

        for (int index = 0; index < originalProfiles.Count; index++)
        {
            SavedOracleConnectionProfile original = originalProfiles[index];
            SavedOracleConnectionProfile normalized = normalizedProfiles[index];
            if (!string.Equals(original.ProfileId, normalized.ProfileId, StringComparison.Ordinal) ||
                !string.Equals(original.Port, normalized.Port, StringComparison.Ordinal) ||
                !string.Equals(original.CredentialTargetId, normalized.CredentialTargetId, StringComparison.Ordinal) ||
                original.OverviewSettings?.IgnorePplsoftModifiedObjects != normalized.OverviewSettings.IgnorePplsoftModifiedObjects ||
                (original.OverviewSettings?.AppPackageTimeoutSeconds ?? 0) != normalized.OverviewSettings.AppPackageTimeoutSeconds ||
                (original.OverviewSettings?.AppEngineTimeoutSeconds ?? 0) != normalized.OverviewSettings.AppEngineTimeoutSeconds ||
                (original.OverviewSettings?.RecordTimeoutSeconds ?? 0) != normalized.OverviewSettings.RecordTimeoutSeconds ||
                (original.OverviewSettings?.PageTimeoutSeconds ?? 0) != normalized.OverviewSettings.PageTimeoutSeconds ||
                (original.OverviewSettings?.ComponentTimeoutSeconds ?? 0) != normalized.OverviewSettings.ComponentTimeoutSeconds)
            {
                return true;
            }
        }

        return false;
    }

    public static string CreateCredentialTargetId(string profileId)
    {
        return $"PeopleCodeIDECompanion:{profileId}";
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
