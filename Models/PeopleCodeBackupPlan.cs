using System;

namespace PeopleCodeIDECompanion.Models;

public sealed class PeopleCodeBackupPlan
{
    public PeopleCodeSourceIdentity Identity { get; init; } = new();

    public string BackupDirectoryPath { get; init; } = string.Empty;

    public string ProposedFileName { get; init; } = string.Empty;

    public DateTimeOffset PlannedAt { get; init; } = DateTimeOffset.UtcNow;

    public string Summary { get; init; } = string.Empty;
}
