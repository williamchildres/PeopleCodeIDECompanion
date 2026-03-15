using System;

namespace PeopleCodeIDECompanion.Models;

public sealed class PeopleCodeCompileLogEntry
{
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    public string Level { get; init; } = "Info";

    public string Message { get; init; } = string.Empty;
}
