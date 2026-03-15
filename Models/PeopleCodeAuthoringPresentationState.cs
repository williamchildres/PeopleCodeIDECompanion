namespace PeopleCodeIDECompanion.Models;

public sealed class PeopleCodeAuthoringPresentationState
{
    public string StatusLabel { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string Detail { get; init; } = string.Empty;

    public string SaveToolTip { get; init; } = string.Empty;

    public string SaveCompileToolTip { get; init; } = string.Empty;

    public string RevertToolTip { get; init; } = string.Empty;

    public bool IsSaveEnabled { get; init; }

    public bool IsSaveCompileEnabled { get; init; }

    public bool IsRevertEnabled { get; init; }
}
