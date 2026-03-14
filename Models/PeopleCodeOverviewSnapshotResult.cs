using System.Collections.Generic;
using System;

namespace PeopleCodeIDECompanion.Models;

public sealed class PeopleCodeOverviewSnapshotResult
{
    public IReadOnlyList<PeopleCodeOverviewItem> RecentUpdates { get; init; } = [];

    public IReadOnlyList<PeopleCodeAuthorActivityItem> RecentAuthors { get; init; } = [];

    public TimeSpan LoadDuration { get; init; }

    public string ErrorMessage { get; init; } = string.Empty;

    public string WarningMessage { get; init; } = string.Empty;
}
