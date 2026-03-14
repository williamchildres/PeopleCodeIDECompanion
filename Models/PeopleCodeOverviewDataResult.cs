using System.Collections.Generic;

namespace PeopleCodeIDECompanion.Models;

public sealed class PeopleCodeOverviewDataResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];

    public string ErrorMessage { get; init; } = string.Empty;

    public string WarningMessage { get; init; } = string.Empty;
}
