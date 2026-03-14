using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;
using PeopleCodeIDECompanion.Models;

namespace PeopleCodeIDECompanion.Services;

public sealed class AppPackageBrowserService
{
    public async Task<AppPackageBrowseResult> GetEntriesAsync(
        OracleConnectionOptions options,
        CancellationToken cancellationToken = default)
    {
        const string query = """
SELECT
    text_keys.OBJECTVALUE1,
    text_keys.OBJECTVALUE2,
    text_keys.OBJECTVALUE3,
    text_keys.OBJECTVALUE4,
    text_keys.OBJECTVALUE5,
    text_keys.OBJECTVALUE6,
    text_keys.OBJECTVALUE7,
    MAX(prog.LASTUPDOPRID) AS LASTUPDOPRID,
    MAX(prog.LASTUPDDTTM) AS LASTUPDDTTM
FROM
(
    SELECT DISTINCT
        OBJECTVALUE1,
        OBJECTVALUE2,
        OBJECTVALUE3,
        OBJECTVALUE4,
        OBJECTVALUE5,
        OBJECTVALUE6,
        OBJECTVALUE7
    FROM PSPCMTXT
    WHERE OBJECTID1 = 104
) text_keys
LEFT JOIN PSPCMPROG prog
    ON prog.OBJECTID1 = 104
    AND prog.OBJECTVALUE1 = text_keys.OBJECTVALUE1
    AND prog.OBJECTVALUE2 = text_keys.OBJECTVALUE2
    AND prog.OBJECTVALUE3 = text_keys.OBJECTVALUE3
    AND prog.OBJECTVALUE4 = text_keys.OBJECTVALUE4
    AND prog.OBJECTVALUE5 = text_keys.OBJECTVALUE5
    AND prog.OBJECTVALUE6 = text_keys.OBJECTVALUE6
    AND prog.OBJECTVALUE7 = text_keys.OBJECTVALUE7
GROUP BY
    text_keys.OBJECTVALUE1,
    text_keys.OBJECTVALUE2,
    text_keys.OBJECTVALUE3,
    text_keys.OBJECTVALUE4,
    text_keys.OBJECTVALUE5,
    text_keys.OBJECTVALUE6,
    text_keys.OBJECTVALUE7
ORDER BY
    UPPER(text_keys.OBJECTVALUE1),
    UPPER(text_keys.OBJECTVALUE2),
    UPPER(text_keys.OBJECTVALUE3),
    UPPER(text_keys.OBJECTVALUE4),
    UPPER(text_keys.OBJECTVALUE5),
    UPPER(text_keys.OBJECTVALUE6),
    UPPER(text_keys.OBJECTVALUE7)
""";

        try
        {
            List<AppPackageEntry> entries = [];

            await using OracleConnection connection = new(OracleConnectionStringFactory.Create(options));
            await connection.OpenAsync(cancellationToken);

            await using OracleCommand command = new(query, connection);
            await using OracleDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                entries.Add(new AppPackageEntry
                {
                    PackageRoot = GetString(reader, 0),
                    ObjectValue2 = GetString(reader, 1),
                    ObjectValue3 = GetString(reader, 2),
                    ObjectValue4 = GetString(reader, 3),
                    ObjectValue5 = GetString(reader, 4),
                    ObjectValue6 = GetString(reader, 5),
                    ObjectValue7 = GetString(reader, 6),
                    LastUpdatedBy = GetString(reader, 7),
                    LastUpdatedDateTime = reader.IsDBNull(8) ? null : reader.GetDateTime(8)
                });
            }

            return new AppPackageBrowseResult
            {
                Entries = entries
            };
        }
        catch (Exception exception)
        {
            return new AppPackageBrowseResult
            {
                ErrorMessage = exception.Message
            };
        }
    }

    public async Task<AppPackageSourceResult> GetSourceAsync(
        OracleConnectionOptions options,
        AppPackageEntry entry,
        CancellationToken cancellationToken = default)
    {
        const string query = """
SELECT PROGSEQ, PCTEXT
FROM PSPCMTXT
WHERE OBJECTID1 = 104
  AND OBJECTVALUE1 = :objectValue1
  AND OBJECTVALUE2 = :objectValue2
  AND OBJECTVALUE3 = :objectValue3
  AND OBJECTVALUE4 = :objectValue4
  AND OBJECTVALUE5 = :objectValue5
  AND OBJECTVALUE6 = :objectValue6
  AND OBJECTVALUE7 = :objectValue7
ORDER BY PROGSEQ
""";

        try
        {
            StringBuilder sourceBuilder = new();

            await using OracleConnection connection = new(OracleConnectionStringFactory.Create(options));
            await connection.OpenAsync(cancellationToken);

            await using OracleCommand command = new(query, connection);
            command.Parameters.Add("objectValue1", OracleDbType.Varchar2, entry.PackageRoot, System.Data.ParameterDirection.Input);
            command.Parameters.Add("objectValue2", OracleDbType.Varchar2, entry.ObjectValue2, System.Data.ParameterDirection.Input);
            command.Parameters.Add("objectValue3", OracleDbType.Varchar2, entry.ObjectValue3, System.Data.ParameterDirection.Input);
            command.Parameters.Add("objectValue4", OracleDbType.Varchar2, entry.ObjectValue4, System.Data.ParameterDirection.Input);
            command.Parameters.Add("objectValue5", OracleDbType.Varchar2, entry.ObjectValue5, System.Data.ParameterDirection.Input);
            command.Parameters.Add("objectValue6", OracleDbType.Varchar2, entry.ObjectValue6, System.Data.ParameterDirection.Input);
            command.Parameters.Add("objectValue7", OracleDbType.Varchar2, entry.ObjectValue7, System.Data.ParameterDirection.Input);

            await using OracleDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                if (sourceBuilder.Length > 0)
                {
                    sourceBuilder.AppendLine();
                }

                sourceBuilder.Append(reader.IsDBNull(1) ? string.Empty : reader.GetString(1));
            }

            return new AppPackageSourceResult
            {
                SourceText = sourceBuilder.ToString()
            };
        }
        catch (Exception exception)
        {
            return new AppPackageSourceResult
            {
                ErrorMessage = exception.Message
            };
        }
    }

    public async Task<AppPackageSourceSearchResult> SearchSourceAsync(
        OracleConnectionOptions options,
        string searchText,
        int maxResults,
        CancellationToken cancellationToken = default)
    {
        const string query = """
WITH matched_rows AS
(
    SELECT
        text_match.OBJECTVALUE1,
        text_match.OBJECTVALUE2,
        text_match.OBJECTVALUE3,
        text_match.OBJECTVALUE4,
        text_match.OBJECTVALUE5,
        text_match.OBJECTVALUE6,
        text_match.OBJECTVALUE7,
        text_match.PROGSEQ,
        text_match.PCTEXT,
        ROW_NUMBER() OVER
        (
            PARTITION BY
                text_match.OBJECTVALUE1,
                text_match.OBJECTVALUE2,
                text_match.OBJECTVALUE3,
                text_match.OBJECTVALUE4,
                text_match.OBJECTVALUE5,
                text_match.OBJECTVALUE6,
                text_match.OBJECTVALUE7
            ORDER BY text_match.PROGSEQ
        ) AS MATCH_ROW_NUMBER
    FROM PSPCMTXT text_match
    WHERE text_match.OBJECTID1 = 104
      AND DBMS_LOB.INSTR(UPPER(text_match.PCTEXT), :searchTextUpper) > 0
),
prog_rows AS
(
    SELECT
        prog.OBJECTVALUE1,
        prog.OBJECTVALUE2,
        prog.OBJECTVALUE3,
        prog.OBJECTVALUE4,
        prog.OBJECTVALUE5,
        prog.OBJECTVALUE6,
        prog.OBJECTVALUE7,
        MAX(prog.LASTUPDOPRID) AS LASTUPDOPRID,
        MAX(prog.LASTUPDDTTM) AS LASTUPDDTTM
    FROM PSPCMPROG prog
    WHERE prog.OBJECTID1 = 104
    GROUP BY
        prog.OBJECTVALUE1,
        prog.OBJECTVALUE2,
        prog.OBJECTVALUE3,
        prog.OBJECTVALUE4,
        prog.OBJECTVALUE5,
        prog.OBJECTVALUE6,
        prog.OBJECTVALUE7
)
SELECT
    search_rows.OBJECTVALUE1,
    search_rows.OBJECTVALUE2,
    search_rows.OBJECTVALUE3,
    search_rows.OBJECTVALUE4,
    search_rows.OBJECTVALUE5,
    search_rows.OBJECTVALUE6,
    search_rows.OBJECTVALUE7,
    search_rows.MATCH_PROGSEQ,
    search_rows.MATCH_PREVIEW,
    search_rows.LASTUPDOPRID,
    search_rows.LASTUPDDTTM
FROM
(
    SELECT
        matched_rows.OBJECTVALUE1,
        matched_rows.OBJECTVALUE2,
        matched_rows.OBJECTVALUE3,
        matched_rows.OBJECTVALUE4,
        matched_rows.OBJECTVALUE5,
        matched_rows.OBJECTVALUE6,
        matched_rows.OBJECTVALUE7,
        matched_rows.PROGSEQ AS MATCH_PROGSEQ,
        matched_rows.PCTEXT AS MATCH_PREVIEW,
        prog_rows.LASTUPDOPRID,
        prog_rows.LASTUPDDTTM
    FROM matched_rows
    LEFT JOIN prog_rows
        ON prog_rows.OBJECTVALUE1 = matched_rows.OBJECTVALUE1
        AND prog_rows.OBJECTVALUE2 = matched_rows.OBJECTVALUE2
        AND prog_rows.OBJECTVALUE3 = matched_rows.OBJECTVALUE3
        AND prog_rows.OBJECTVALUE4 = matched_rows.OBJECTVALUE4
        AND prog_rows.OBJECTVALUE5 = matched_rows.OBJECTVALUE5
        AND prog_rows.OBJECTVALUE6 = matched_rows.OBJECTVALUE6
        AND prog_rows.OBJECTVALUE7 = matched_rows.OBJECTVALUE7
    WHERE matched_rows.MATCH_ROW_NUMBER = 1
    ORDER BY
        UPPER(matched_rows.OBJECTVALUE1),
        UPPER(matched_rows.OBJECTVALUE2),
        UPPER(matched_rows.OBJECTVALUE3),
        UPPER(matched_rows.OBJECTVALUE4),
        UPPER(matched_rows.OBJECTVALUE5),
        UPPER(matched_rows.OBJECTVALUE6),
        UPPER(matched_rows.OBJECTVALUE7)
) search_rows
WHERE ROWNUM <= :maxResults
""";

        try
        {
            List<AppPackageSourceSearchMatch> matches = [];

            await using OracleConnection connection = new(OracleConnectionStringFactory.Create(options));
            await connection.OpenAsync(cancellationToken);

            await using OracleCommand command = new(query, connection);
            command.BindByName = true;
            command.Parameters.Add(
                "searchTextUpper",
                OracleDbType.Varchar2,
                searchText.ToUpperInvariant(),
                System.Data.ParameterDirection.Input);
            command.Parameters.Add("maxResults", OracleDbType.Int32, maxResults, System.Data.ParameterDirection.Input);

            await using OracleDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                AppPackageEntry entry = new()
                {
                    PackageRoot = GetString(reader, 0),
                    ObjectValue2 = GetString(reader, 1),
                    ObjectValue3 = GetString(reader, 2),
                    ObjectValue4 = GetString(reader, 3),
                    ObjectValue5 = GetString(reader, 4),
                    ObjectValue6 = GetString(reader, 5),
                    ObjectValue7 = GetString(reader, 6),
                    LastUpdatedBy = GetString(reader, 9),
                    LastUpdatedDateTime = reader.IsDBNull(10) ? null : reader.GetDateTime(10)
                };

                matches.Add(new AppPackageSourceSearchMatch
                {
                    Entry = entry,
                    MatchSequence = reader.IsDBNull(7) ? 0 : Convert.ToInt32(reader.GetValue(7)),
                    MatchPreview = NormalizePreview(GetString(reader, 8))
                });
            }

            return new AppPackageSourceSearchResult
            {
                Matches = matches,
                WasLimited = matches.Count >= maxResults
            };
        }
        catch (Exception exception)
        {
            return new AppPackageSourceSearchResult
            {
                ErrorMessage = exception.Message
            };
        }
    }

    private static string GetString(OracleDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
    }

    private static string NormalizePreview(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "(matching source row was blank)"
            : value.Trim().Replace('\t', ' ');
    }
}
