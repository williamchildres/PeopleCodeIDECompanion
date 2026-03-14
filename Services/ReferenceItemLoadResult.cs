using System.Collections.Generic;
using PeopleCodeIDECompanion.Models;

namespace PeopleCodeIDECompanion.Services;

public sealed class ReferenceItemLoadResult
{
    public static ReferenceItemLoadResult Success(IReadOnlyList<ReferenceItem> items) => new()
    {
        Items = items
    };

    public static ReferenceItemLoadResult Failure(string errorMessage) => new()
    {
        ErrorMessage = errorMessage
    };

    public IReadOnlyList<ReferenceItem> Items { get; init; } = [];

    public string ErrorMessage { get; init; } = string.Empty;
}
