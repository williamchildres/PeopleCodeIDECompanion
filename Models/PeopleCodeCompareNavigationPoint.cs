namespace PeopleCodeIDECompanion.Models;

public sealed class PeopleCodeCompareNavigationPoint
{
    public int LineIndex { get; init; }

    public CharacterRange? LeftRange { get; init; }

    public CharacterRange? RightRange { get; init; }
}
