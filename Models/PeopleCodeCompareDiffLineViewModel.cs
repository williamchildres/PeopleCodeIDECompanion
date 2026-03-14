using Microsoft.UI.Xaml.Media;

namespace PeopleCodeIDECompanion.Models;

public sealed class PeopleCodeCompareDiffLineViewModel
{
    public int? LeftLineNumber { get; init; }

    public string LeftText { get; init; } = string.Empty;

    public Brush LeftBackground { get; init; } = null!;

    public int? RightLineNumber { get; init; }

    public string RightText { get; init; } = string.Empty;

    public Brush RightBackground { get; init; } = null!;
}
