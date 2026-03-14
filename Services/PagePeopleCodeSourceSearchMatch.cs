using PeopleCodeIDECompanion.Models;

namespace PeopleCodeIDECompanion.Services;

public sealed class PagePeopleCodeSourceSearchMatch
{
    public PagePeopleCodeItem Item { get; init; } = new();

    public int MatchSequence { get; init; }

    public string MatchPreview { get; init; } = string.Empty;

    public string DisplayLabel =>
        $"{Item.PageName} | {Item.DisplayName} | {Item.StructureLabel}";
}
