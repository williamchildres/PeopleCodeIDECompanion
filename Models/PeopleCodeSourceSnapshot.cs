using System;

namespace PeopleCodeIDECompanion.Models;

public sealed class PeopleCodeSourceSnapshot
{
    public PeopleCodeSourceIdentity Identity { get; init; } = new();

    public string SourceText { get; init; } = string.Empty;

    public string MetadataSummary { get; init; } = string.Empty;

    public string LastUpdatedBy { get; init; } = string.Empty;

    public DateTime? LastUpdatedDateTime { get; init; }

    public DateTimeOffset CapturedAt { get; init; } = DateTimeOffset.UtcNow;
}
