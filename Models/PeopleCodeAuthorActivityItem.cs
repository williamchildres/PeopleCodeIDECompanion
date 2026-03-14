using System;

namespace PeopleCodeIDECompanion.Models;

public sealed class PeopleCodeAuthorActivityItem
{
    public string Oprid { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public DateTime? MostRecentUpdateDateTime { get; init; }

    public int UpdateCount { get; init; }

    public string Title => string.IsNullOrWhiteSpace(DisplayName) ? Oprid : DisplayName;

    public string Subtitle =>
        $"{(MostRecentUpdateDateTime?.ToString("g") ?? "Unknown time")} | {UpdateCount} update(s)";
}
