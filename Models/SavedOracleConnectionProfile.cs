namespace PeopleCodeIDECompanion.Models;

public sealed class SavedOracleConnectionProfile
{
    public string DisplayName { get; set; } = string.Empty;

    public string Host { get; set; } = string.Empty;

    public string Port { get; set; } = "1521";

    public string ServiceName { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;
}
