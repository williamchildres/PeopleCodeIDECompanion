using Oracle.ManagedDataAccess.Client;
using PeopleCodeIDECompanion.Models;

namespace PeopleCodeIDECompanion.Services;

public static class OracleConnectionStringFactory
{
    public static string Create(OracleConnectionOptions options)
    {
        OracleConnectionStringBuilder connectionStringBuilder = new()
        {
            DataSource =
                $"(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST={options.Host.Trim()})(PORT={options.Port.Trim()}))(CONNECT_DATA=(SERVICE_NAME={options.ServiceName.Trim()})))",
            UserID = options.Username.Trim(),
            Password = options.Password,
            ConnectionTimeout = 15
        };

        return connectionStringBuilder.ConnectionString;
    }
}
