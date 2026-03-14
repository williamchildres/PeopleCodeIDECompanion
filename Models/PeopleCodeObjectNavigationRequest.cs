namespace PeopleCodeIDECompanion.Models;

public sealed class PeopleCodeObjectNavigationRequest
{
    public string ProfileId { get; init; } = string.Empty;

    public string ObjectType { get; init; } = string.Empty;

    public object? SourceKey { get; init; }
}
