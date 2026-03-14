using System;
using System.Threading;
using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;
using PeopleCodeIDECompanion.Models;

namespace PeopleCodeIDECompanion.Services;

public sealed class OracleConnectionTester
{
    public async Task<OracleConnectionTestResult> TestConnectionAsync(
        OracleConnectionOptions options,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.Host) ||
            string.IsNullOrWhiteSpace(options.Port) ||
            string.IsNullOrWhiteSpace(options.ServiceName) ||
            string.IsNullOrWhiteSpace(options.Username) ||
            string.IsNullOrWhiteSpace(options.Password))
        {
            return OracleConnectionTestResult.Failure("Enter host, port, service name, username, and password.");
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            using OracleConnection connection = new(OracleConnectionStringFactory.Create(options));
            await connection.OpenAsync();

            string serverVersion = connection.ServerVersion;
            string dataSource = connection.DataSource;

            return OracleConnectionTestResult.Success(
                $"Connection succeeded.{Environment.NewLine}Data Source: {dataSource}{Environment.NewLine}Server Version: {serverVersion}");
        }
        catch (Exception exception)
        {
            return OracleConnectionTestResult.Failure(exception.Message);
        }
    }
}
