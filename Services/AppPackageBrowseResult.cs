using System.Collections.Generic;
using PeopleCodeIDECompanion.Models;

namespace PeopleCodeIDECompanion.Services;

public sealed class AppPackageBrowseResult
{
    public IReadOnlyList<AppPackageEntry> Entries { get; init; } = [];

    public string ErrorMessage { get; init; } = string.Empty;
}
