namespace PeopleCodeIDECompanion.Models;

public sealed class OracleConnectionSession
{
    public string ProfileId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string CredentialTargetId { get; set; } = string.Empty;

    public OracleConnectionOptions Options { get; set; } = new();

    public PeopleCodeOverviewProfileSettings OverviewSettings { get; set; } = new();
}
