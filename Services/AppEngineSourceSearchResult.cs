using System.Collections.Generic;

namespace PeopleCodeIDECompanion.Services;

public sealed class AppEngineSourceSearchResult
{
    public IReadOnlyList<AppEngineSourceSearchMatch> Matches { get; init; } = [];

    public bool WasLimited { get; init; }

    public string ErrorMessage { get; init; } = string.Empty;
}
