using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;

namespace PeopleCodeIDECompanion.Services;

public sealed class SecureCredentialStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("PeopleCodeIDECompanion.OracleProfileCredential");
    private readonly string _credentialsDirectory;

    public SecureCredentialStore()
    {
        string appDataDirectory = ResolveAppDataDirectory();
        _credentialsDirectory = Path.Combine(appDataDirectory, "Credentials");
    }

    public async Task SavePasswordAsync(
        string credentialTargetId,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(credentialTargetId))
        {
            throw new ArgumentException("Credential target is required.", nameof(credentialTargetId));
        }

        Directory.CreateDirectory(_credentialsDirectory);
        byte[] plaintextBytes = Encoding.UTF8.GetBytes(password ?? string.Empty);
        byte[] protectedBytes = ProtectedData.Protect(plaintextBytes, Entropy, DataProtectionScope.CurrentUser);
        await File.WriteAllBytesAsync(GetCredentialPath(credentialTargetId), protectedBytes, cancellationToken);
    }

    public async Task<string?> LoadPasswordAsync(string credentialTargetId, CancellationToken cancellationToken = default)
    {
        string path = GetCredentialPath(credentialTargetId);
        if (!File.Exists(path))
        {
            return null;
        }

        byte[] protectedBytes = await File.ReadAllBytesAsync(path, cancellationToken);
        byte[] plaintextBytes = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(plaintextBytes);
    }

    public Task<bool> HasPasswordAsync(string credentialTargetId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(File.Exists(GetCredentialPath(credentialTargetId)));
    }

    public Task DeletePasswordAsync(string credentialTargetId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string path = GetCredentialPath(credentialTargetId);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string GetCredentialPath(string credentialTargetId)
    {
        string safeFileName = credentialTargetId.Replace(':', '_');
        return Path.Combine(_credentialsDirectory, $"{safeFileName}.bin");
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
