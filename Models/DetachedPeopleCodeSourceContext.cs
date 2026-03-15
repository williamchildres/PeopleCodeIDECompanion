namespace PeopleCodeIDECompanion.Models;

public sealed class DetachedPeopleCodeSourceContext
{
    public string WindowTitle { get; init; } = string.Empty;

    public string ObjectType { get; init; } = string.Empty;

    public string ObjectTitle { get; init; } = string.Empty;

    public string ObjectSubtitle { get; init; } = string.Empty;

    public string ProfileContext { get; init; } = string.Empty;

    public string MetadataSummary { get; init; } = string.Empty;

    public string LastUpdatedText { get; init; } = string.Empty;

    public string SourceText { get; init; } = string.Empty;

    public string? SearchText { get; init; }

    public bool UseSyntaxHighlighting { get; init; } = true;

    public PeopleCodeSourceIdentity SourceIdentity { get; init; } = new();

    public PeopleCodeAuthoringCapabilitySnapshot AuthoringCapabilities { get; init; } = new();
}
