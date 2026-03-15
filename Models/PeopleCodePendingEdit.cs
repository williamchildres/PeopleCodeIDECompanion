using System;

namespace PeopleCodeIDECompanion.Models;

public sealed class PeopleCodePendingEdit
{
    public PeopleCodeSourceSnapshot OriginalSnapshot { get; init; } = new();

    public string EditedSourceText { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public bool HasChanges => !string.Equals(
        OriginalSnapshot.SourceText,
        EditedSourceText,
        StringComparison.Ordinal);
}
