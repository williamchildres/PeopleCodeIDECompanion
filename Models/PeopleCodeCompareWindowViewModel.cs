using System.Collections.Generic;

namespace PeopleCodeIDECompanion.Models;

public sealed class PeopleCodeCompareWindowViewModel
{
    public string WindowTitle { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Subtitle { get; init; } = string.Empty;

    public string DiffSummary { get; init; } = string.Empty;

    public string DiffNotice { get; init; } = string.Empty;

    public bool HasDiffNotice => !string.IsNullOrWhiteSpace(DiffNotice);

    public PeopleCodeComparePaneViewModel LeftPane { get; init; } = new();

    public PeopleCodeComparePaneViewModel RightPane { get; init; } = new();

    public IReadOnlyList<PeopleCodeCompareNavigationPoint> DiffNavigationPoints { get; init; } =
        System.Array.Empty<PeopleCodeCompareNavigationPoint>();

    public int DiffCount => DiffNavigationPoints.Count;

    public IReadOnlyList<PeopleCodeCompareDiffLineViewModel> DiffLines { get; init; } =
        System.Array.Empty<PeopleCodeCompareDiffLineViewModel>();
}
