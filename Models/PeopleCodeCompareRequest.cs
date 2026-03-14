namespace PeopleCodeIDECompanion.Models;

public sealed class PeopleCodeCompareRequest
{
    public OracleConnectionSession LeftSession { get; init; } = new();

    public OracleConnectionSession RightSession { get; init; } = new();

    public PeopleCodeSourceDescriptor SourceDescriptor { get; init; } = new();

    public string LeftSourceText { get; init; } = string.Empty;
}
