using System.Collections.Generic;

namespace PeopleCodeIDECompanion.Services;

public sealed class RecordPeopleCodeSourceSearchResult
{
    public IReadOnlyList<RecordPeopleCodeSourceSearchMatch> Matches { get; init; } = [];

    public bool WasLimited { get; init; }

    public string ErrorMessage { get; init; } = string.Empty;
}
