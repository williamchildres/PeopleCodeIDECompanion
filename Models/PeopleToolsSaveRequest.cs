namespace PeopleCodeIDECompanion.Models;

public sealed class PeopleToolsSaveRequest
{
    public PeopleCodeSourceSnapshot Snapshot { get; init; } = new();

    public string EditedSourceText { get; init; } = string.Empty;

    public string PreferredToolPath { get; init; } = string.Empty;
}
