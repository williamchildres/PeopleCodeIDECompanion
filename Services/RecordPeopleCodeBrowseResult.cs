using System.Collections.Generic;
using PeopleCodeIDECompanion.Models;

namespace PeopleCodeIDECompanion.Services;

public sealed class RecordPeopleCodeBrowseResult
{
    public IReadOnlyList<RecordPeopleCodeItem> Items { get; init; } = [];

    public string ErrorMessage { get; init; } = string.Empty;
}
