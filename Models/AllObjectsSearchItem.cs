using System;

namespace PeopleCodeIDECompanion.Models;

public sealed class AllObjectsSearchItem
{
    public string ObjectType { get; init; } = string.Empty;

    public string PrimaryText { get; init; } = string.Empty;

    public string SecondaryText { get; init; } = string.Empty;

    public string MetadataTitle { get; init; } = string.Empty;

    public string MetadataSubtitle { get; init; } = string.Empty;

    public string MetadataSummary { get; init; } = string.Empty;

    public string MatchPreview { get; init; } = string.Empty;

    public string LastUpdatedBy { get; init; } = string.Empty;

    public DateTime? LastUpdatedDateTime { get; init; }

    public object SourceKey { get; init; } = new();
}
