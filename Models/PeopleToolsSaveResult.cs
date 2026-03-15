namespace PeopleCodeIDECompanion.Models;

public sealed class PeopleToolsSaveResult
{
    public bool WasAttempted { get; init; }

    public bool IsSuccess { get; init; }

    public string Summary { get; init; } = string.Empty;

    public string Detail { get; init; } = string.Empty;
}
