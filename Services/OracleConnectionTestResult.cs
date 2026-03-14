namespace PeopleCodeIDECompanion.Services;

public sealed class OracleConnectionTestResult
{
    public static OracleConnectionTestResult Success(string details) => new()
    {
        IsSuccess = true,
        Details = details
    };

    public static OracleConnectionTestResult Failure(string details) => new()
    {
        IsSuccess = false,
        Details = details
    };

    public bool IsSuccess { get; init; }

    public string Details { get; init; } = string.Empty;
}
