using System.Collections.Generic;
using PeopleCodeIDECompanion.Models;

namespace PeopleCodeIDECompanion.Services;

public sealed class ComponentPeopleCodeBrowseResult
{
    public IReadOnlyList<ComponentPeopleCodeItem> Items { get; init; } = [];

    public string ErrorMessage { get; init; } = string.Empty;
}
