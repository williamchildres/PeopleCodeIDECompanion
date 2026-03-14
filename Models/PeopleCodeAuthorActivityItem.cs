using System;

namespace PeopleCodeIDECompanion.Models;

public sealed class PeopleCodeAuthorActivityItem
{
    public string Oprid { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public DateTime? MostRecentUpdateDateTime { get; init; }

    public int UpdateCount { get; init; }

    public string DisplayLabel => string.IsNullOrWhiteSpace(DisplayName) ? Oprid : DisplayName;

    public string OpridLabel =>
        string.IsNullOrWhiteSpace(Oprid)
            ? "OPRID unavailable"
            : $"OPRID: {Oprid}";

    public string ActivitySummary =>
        $"{(MostRecentUpdateDateTime?.ToString("g") ?? "Unknown time")} | {UpdateCount} update(s)";

    public string HeaderLabel =>
        string.IsNullOrWhiteSpace(DisplayName) || DisplayName.Equals(Oprid, StringComparison.OrdinalIgnoreCase)
            ? Oprid
            : DisplayName.Contains(Oprid, StringComparison.OrdinalIgnoreCase)
                ? DisplayName
                : $"{DisplayName} ({Oprid})";

    public string Title => DisplayLabel;

    public string Subtitle => ActivitySummary;
}
