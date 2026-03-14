using System.Collections.Generic;
using PeopleCodeIDECompanion.Models;

namespace PeopleCodeIDECompanion.Services;

public sealed class AllObjectsSearchResult
{
    public IReadOnlyList<AllObjectsSearchGroup> Groups { get; init; } = [];

    public IReadOnlyList<AllObjectsSearchItem> Items { get; init; } = [];

    public IReadOnlyList<string> FailureMessages { get; init; } = [];

    public bool WasLimited { get; init; }
}
