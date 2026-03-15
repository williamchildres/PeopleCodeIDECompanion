using System.Collections.Generic;

namespace PeopleCodeIDECompanion.Models;

public sealed class PeopleCodeSaveResult
{
    public bool WasAttempted { get; init; }

    public bool IsSuccess { get; init; }

    public string Message { get; init; } = string.Empty;

    public PeopleCodeBackupPlan? BackupPlan { get; init; }

    public IReadOnlyList<PeopleCodeWriteBackDiscoveryPoint> DiscoveryPoints { get; init; } = [];
}
