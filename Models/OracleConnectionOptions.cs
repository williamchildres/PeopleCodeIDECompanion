namespace PeopleCodeIDECompanion.Models;

public sealed class OracleConnectionOptions
{
    public string Host { get; init; } = string.Empty;

    public string Port { get; init; } = "1521";

    public string ServiceName { get; init; } = string.Empty;

    public string Username { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;
}
