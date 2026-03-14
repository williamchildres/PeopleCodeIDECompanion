namespace PeopleCodeIDECompanion.Models;

public sealed class PeopleCodeRepeatedCodeSearchOptions
{
    public int MinimumLinesPerBlock { get; init; } = 4;

    public int MinimumCharactersPerBlock { get; init; } = 80;

    public int MaximumResults { get; init; } = 20;

    public int MaximumObjectsToScan { get; init; } = 80;
}
