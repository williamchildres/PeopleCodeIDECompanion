namespace PeopleCodeIDECompanion.Models;

public sealed class PeopleCodeSourceDescriptor
{
    public PeopleCodeSourceIdentity Identity { get; init; } = new();

    public string ObjectTitle { get; init; } = string.Empty;

    public string ObjectSubtitle { get; init; } = string.Empty;

    public string MetadataSummary { get; init; } = string.Empty;

    public bool UseSyntaxHighlighting { get; init; } = true;
}
