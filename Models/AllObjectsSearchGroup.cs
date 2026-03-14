namespace PeopleCodeIDECompanion.Models;

public sealed class AllObjectsSearchGroup
{
    public string ObjectType { get; init; } = string.Empty;

    public int MatchCount { get; init; }

    public string DisplayLabel => $"{ObjectType} ({MatchCount})";
}
