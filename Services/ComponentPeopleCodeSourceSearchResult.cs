using System.Collections.Generic;

namespace PeopleCodeIDECompanion.Services;

public sealed class ComponentPeopleCodeSourceSearchResult
{
    public IReadOnlyList<ComponentPeopleCodeSourceSearchMatch> Matches { get; init; } = [];

    public bool WasLimited { get; init; }

    public string ErrorMessage { get; init; } = string.Empty;
}
