namespace PeopleCodeIDECompanion.Models;

public sealed class PeopleCodeRepeatedCodeOccurrence
{
    public PeopleCodeOverviewItem Location { get; init; } = new();

    public string Subtitle => $"{Location.ObjectType} | {Location.Subtitle}";
}
