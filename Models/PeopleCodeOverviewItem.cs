using System;

namespace PeopleCodeIDECompanion.Models;

public sealed class PeopleCodeOverviewItem
{
    public string ObjectType { get; init; } = string.Empty;

    public string ObjectName { get; init; } = string.Empty;

    public string Descriptor { get; init; } = string.Empty;

    public string LastUpdatedBy { get; init; } = string.Empty;

    public DateTime? LastUpdatedDateTime { get; init; }

    public object? SourceKey { get; init; }

    public string Title =>
        string.IsNullOrWhiteSpace(ObjectName)
            ? $"({ObjectType})"
            : $"{ObjectName} ({ObjectType})";

    public string Subtitle => string.IsNullOrWhiteSpace(Descriptor) ? "No additional descriptor" : Descriptor;

    public string LastUpdatedDisplay =>
        $"{(LastUpdatedDateTime?.ToString("g") ?? "Unknown time")} | {(string.IsNullOrWhiteSpace(LastUpdatedBy) ? "Unknown OPRID" : LastUpdatedBy)}";
}
