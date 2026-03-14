using System.Collections.Generic;

namespace PeopleCodeIDECompanion.Models;

public sealed class PeopleCodeRepeatedCodeSearchResult
{
    public IReadOnlyList<PeopleCodeRepeatedCodeBlock> Blocks { get; init; } = [];

    public string ErrorMessage { get; init; } = string.Empty;

    public string WarningMessage { get; init; } = string.Empty;

    public int ScannedObjectCount { get; init; }

    public bool WasObjectScanLimited { get; init; }

    public string Summary =>
        WasObjectScanLimited
            ? $"Scanned {ScannedObjectCount} objects (bounded scan)."
            : $"Scanned {ScannedObjectCount} objects.";
}
