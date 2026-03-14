namespace PeopleCodeIDECompanion.Models;

public sealed class PeopleCodeSourceIdentity
{
    public string ObjectType { get; init; } = string.Empty;

    public object SourceKey { get; init; } = new();
}
