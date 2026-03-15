using System;
using System.IO;
using PeopleCodeIDECompanion.Models;
using Windows.Storage;

namespace PeopleCodeIDECompanion.Services;

public sealed class PlaceholderPeopleCodeBackupService : IPeopleCodeBackupService
{
    public PeopleCodeBackupPlan CreateBackupPlan(PeopleCodeSourceSnapshot snapshot)
    {
        string backupDirectory = ResolveBackupDirectory();
        string safeIdentity = SanitizeFileSegment(snapshot.Identity.ObjectTitle);
        if (string.IsNullOrWhiteSpace(safeIdentity))
        {
            safeIdentity = snapshot.Identity.ObjectType;
        }

        string fileName = $"{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{safeIdentity}.pcode.bak";
        return new PeopleCodeBackupPlan
        {
            Identity = snapshot.Identity,
            BackupDirectoryPath = backupDirectory,
            ProposedFileName = fileName,
            Summary = "Backup execution is not enabled yet, but future save-back should write the original source to the local Backups folder before any database change."
        };
    }

    private static string ResolveBackupDirectory()
    {
        try
        {
            return Path.Combine(ApplicationData.Current.LocalFolder.Path, "Backups");
        }
        catch
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PeopleCodeIDECompanion",
                "Backups");
        }
    }

    private static string SanitizeFileSegment(string value)
    {
        string safe = value ?? string.Empty;
        foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
        {
            safe = safe.Replace(invalidCharacter, '_');
        }

        return safe.Trim();
    }
}
