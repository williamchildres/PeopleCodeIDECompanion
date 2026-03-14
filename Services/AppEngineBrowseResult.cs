using System.Collections.Generic;
using PeopleCodeIDECompanion.Models;

namespace PeopleCodeIDECompanion.Services;

public sealed class AppEngineBrowseResult
{
    public IReadOnlyList<AppEngineItem> Items { get; init; } = [];

    public string ErrorMessage { get; init; } = string.Empty;
}
