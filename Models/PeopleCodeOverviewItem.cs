using System;

namespace PeopleCodeIDECompanion.Models;

public sealed class PeopleCodeOverviewItem
{
    public string ObjectType { get; init; } = string.Empty;

    public string ObjectName { get; init; } = string.Empty;

    public string Descriptor { get; init; } = string.Empty;

    public string LastUpdatedBy { get; init; } = string.Empty;

    public string LastUpdatedByDisplay { get; init; } = string.Empty;

    public DateTime? LastUpdatedDateTime { get; init; }

    public object? SourceKey { get; init; }

    public string PrimaryLabel =>
        string.IsNullOrWhiteSpace(ObjectName)
            ? "(Unnamed object)"
            : ObjectName;

    public string ObjectTypeBadge =>
        string.IsNullOrWhiteSpace(ObjectType)
            ? "PeopleCode"
            : ObjectType;

    public string DescriptorLabel =>
        string.IsNullOrWhiteSpace(Descriptor)
            ? "No source descriptor"
            : Descriptor;

    public string AttributionLabel =>
        $"{(LastUpdatedDateTime?.ToString("g") ?? "Unknown time")} | {GetLastUpdatedIdentityLabel()}";

    public string Title => PrimaryLabel;

    public string Subtitle => DescriptorLabel;

    public string LastUpdatedDisplay => AttributionLabel;

    private string GetLastUpdatedIdentityLabel()
    {
        if (!string.IsNullOrWhiteSpace(LastUpdatedBy) &&
            !string.IsNullOrWhiteSpace(LastUpdatedByDisplay) &&
            !LastUpdatedBy.Equals(LastUpdatedByDisplay, StringComparison.OrdinalIgnoreCase) &&
            !LastUpdatedByDisplay.Contains(LastUpdatedBy, StringComparison.OrdinalIgnoreCase))
        {
            return $"{LastUpdatedBy} ({LastUpdatedByDisplay})";
        }

        if (!string.IsNullOrWhiteSpace(LastUpdatedByDisplay))
        {
            return LastUpdatedByDisplay;
        }

        return string.IsNullOrWhiteSpace(LastUpdatedBy) ? "Unknown OPRID" : LastUpdatedBy;
    }
}
