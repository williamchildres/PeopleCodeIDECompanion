namespace PeopleCodeIDECompanion.Models;

public sealed class PeopleCodeSourceIdentity
{
    public string ProfileId { get; init; } = string.Empty;

    public string ObjectType { get; init; } = string.Empty;

    public string ObjectTitle { get; init; } = string.Empty;

    public object SourceKey { get; init; } = new();

    public PeopleCodeAuthoritativeIdentity? AuthoritativeIdentity { get; init; }
}
