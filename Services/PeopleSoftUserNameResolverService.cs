using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;
using PeopleCodeIDECompanion.Models;

namespace PeopleCodeIDECompanion.Services;

public sealed class PeopleSoftUserNameResolverService
{
    private readonly Dictionary<string, string> _displayNameCache = new(StringComparer.OrdinalIgnoreCase);

    public async Task<string> GetDisplayLabelAsync(
        OracleConnectionOptions options,
        string oprid,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(oprid) || !oprid.All(char.IsDigit))
        {
            return oprid;
        }

        if (_displayNameCache.TryGetValue(oprid, out string? cachedDisplayLabel))
        {
            return cachedDisplayLabel;
        }

        string preferredName = await ResolvePreferredNameAsync(options, oprid, cancellationToken);
        string displayLabel = string.IsNullOrWhiteSpace(preferredName)
            ? oprid
            : $"{preferredName} ({oprid})";

        _displayNameCache[oprid] = displayLabel;
        return displayLabel;
    }

    private static async Task<string> ResolvePreferredNameAsync(
        OracleConnectionOptions options,
        string oprid,
        CancellationToken cancellationToken)
    {
        try
        {
            await using OracleConnection connection = new(OracleConnectionStringFactory.Create(options));
            await connection.OpenAsync(cancellationToken);

            await using OracleCommand command = connection.CreateCommand();
            command.BindByName = true;
            command.CommandText = """
SELECT NAME_TYPE, NAME
FROM PS_NAMES
WHERE EMPLID = :emplid
""";
            command.Parameters.Add("emplid", OracleDbType.Varchar2, oprid, System.Data.ParameterDirection.Input);

            List<(string NameType, string Name)> names = [];
            await using OracleDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                string nameType = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                string name = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    names.Add((nameType, name));
                }
            }

            return SelectPreferredName(names);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string SelectPreferredName(IReadOnlyList<(string NameType, string Name)> names)
    {
        (string NameType, string Name)? preferred = names.FirstOrDefault(name =>
            name.NameType.Equals("PRF", StringComparison.OrdinalIgnoreCase));
        if (preferred is not null && !string.IsNullOrWhiteSpace(preferred.Value.Name))
        {
            return preferred.Value.Name;
        }

        preferred = names.FirstOrDefault(name =>
            name.NameType.Equals("PRI", StringComparison.OrdinalIgnoreCase));
        if (preferred is not null && !string.IsNullOrWhiteSpace(preferred.Value.Name))
        {
            return preferred.Value.Name;
        }

        return names
            .Select(name => name.Name)
            .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name))
            ?? string.Empty;
    }
}
