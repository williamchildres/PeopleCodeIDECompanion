using System.Collections.Generic;

namespace PeopleCodeIDECompanion.Services;

public sealed class AppPackageSourceSearchResult
{
    public IReadOnlyList<AppPackageSourceSearchMatch> Matches { get; init; } = [];

    public bool WasLimited { get; init; }

    public string ErrorMessage { get; init; } = string.Empty;
}
