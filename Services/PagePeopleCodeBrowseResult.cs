using System.Collections.Generic;
using PeopleCodeIDECompanion.Models;

namespace PeopleCodeIDECompanion.Services;

public sealed class PagePeopleCodeBrowseResult
{
    public IReadOnlyList<PagePeopleCodeItem> Items { get; init; } = [];

    public string ErrorMessage { get; init; } = string.Empty;
}
