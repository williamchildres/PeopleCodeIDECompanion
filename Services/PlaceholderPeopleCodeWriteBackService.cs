using System.Threading;
using System.Threading.Tasks;
using PeopleCodeIDECompanion.Models;

namespace PeopleCodeIDECompanion.Services;

public sealed class PlaceholderPeopleCodeWriteBackService : IPeopleCodeWriteBackService
{
    private readonly IPeopleCodeBackupService _backupService;

    public PlaceholderPeopleCodeWriteBackService(IPeopleCodeBackupService backupService)
    {
        _backupService = backupService;
    }

    public Task<PeopleCodeSaveResult> SaveAsync(PeopleCodePendingEdit pendingEdit, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PeopleCodeBackupPlan backupPlan = _backupService.CreateBackupPlan(pendingEdit.OriginalSnapshot);
        bool isAppPackage = pendingEdit.OriginalSnapshot.Identity.ObjectType == AllObjectsPeopleCodeBrowserService.AppPackageMode;

        return Task.FromResult(new PeopleCodeSaveResult
        {
            WasAttempted = false,
            IsSuccess = false,
            Message = isAppPackage
                ? "Direct App Package database save is intentionally disabled. PSPCMPROG is authoritative and stores binary PROGTXT, so the next safe path is PeopleTools-backed save rather than direct DML."
                : "Write-back is intentionally disabled in this build. Validate object-specific table/key rules and execute the planned backup before enabling database updates.",
            BackupPlan = backupPlan,
            DiscoveryPoints = PeopleCodeWriteBackDiscoveryCatalog.ForObjectType(pendingEdit.OriginalSnapshot.Identity.ObjectType)
        });
    }
}
