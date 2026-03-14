namespace PeopleCodeIDECompanion.Models;

public sealed class OracleConnectionSession
{
    public string DisplayName { get; init; } = string.Empty;

    public OracleConnectionOptions Options { get; init; } = new();
}
