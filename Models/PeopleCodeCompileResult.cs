using System.Collections.Generic;

namespace PeopleCodeIDECompanion.Models;

public sealed class PeopleCodeCompileResult
{
    public bool WasAttempted { get; init; }

    public bool IsSuccess { get; init; }

    public string Message { get; init; } = string.Empty;

    public IReadOnlyList<PeopleCodeCompileLogEntry> LogEntries { get; init; } = [];
}
