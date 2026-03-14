namespace PeopleCodeIDECompanion.Models;

public sealed class PeopleCodeComparePaneViewModel
{
    public string ProfileDisplayName { get; init; } = string.Empty;

    public string ProfileContext { get; init; } = string.Empty;

    public string ObjectType { get; init; } = string.Empty;

    public string ObjectTitle { get; init; } = string.Empty;

    public string ObjectSubtitle { get; init; } = string.Empty;

    public string MetadataSummary { get; init; } = string.Empty;

    public string StatusMessage { get; init; } = string.Empty;

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);
}
