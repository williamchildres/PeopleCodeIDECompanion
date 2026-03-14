using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;
using PeopleCodeIDECompanion.Models;

namespace PeopleCodeIDECompanion.Services;

public sealed class AppEngineBrowserService
{
    public async Task<AppEngineBrowseResult> GetItemsAsync(
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
    WHERE OBJECTID1 = 66
      AND NVL(TRIM(OBJECTVALUE1), ' ') <> ' '
      AND NVL(TRIM(OBJECTVALUE2), ' ') <> ' '
      AND NVL(TRIM(OBJECTVALUE6), ' ') <> ' '
) text_keys
LEFT JOIN PSPCMPROG prog
    ON prog.OBJECTID1 = 66
    AND NVL(prog.OBJECTVALUE1, ' ') = NVL(text_keys.OBJECTVALUE1, ' ')
    AND NVL(prog.OBJECTVALUE2, ' ') = NVL(text_keys.OBJECTVALUE2, ' ')
    AND NVL(prog.OBJECTVALUE3, ' ') = NVL(text_keys.OBJECTVALUE3, ' ')
    AND NVL(prog.OBJECTVALUE4, ' ') = NVL(text_keys.OBJECTVALUE4, ' ')
    AND NVL(prog.OBJECTVALUE5, ' ') = NVL(text_keys.OBJECTVALUE5, ' ')
    AND NVL(prog.OBJECTVALUE6, ' ') = NVL(text_keys.OBJECTVALUE6, ' ')
    AND NVL(prog.OBJECTVALUE7, ' ') = NVL(text_keys.OBJECTVALUE7, ' ')
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
    UPPER(text_keys.OBJECTVALUE6),
    UPPER(text_keys.OBJECTVALUE7),
    UPPER(text_keys.OBJECTVALUE3),
    UPPER(text_keys.OBJECTVALUE4),
    UPPER(text_keys.OBJECTVALUE5)
""";

        try
        {
            List<AppEngineItem> items = [];

            await using OracleConnection connection = new(OracleConnectionStringFactory.Create(options));
            await connection.OpenAsync(cancellationToken);

            await using OracleCommand command = new(query, connection);
            await using OracleDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(new AppEngineItem
                {
                    ProgramName = GetString(reader, 0),
                    SectionName = GetString(reader, 1),
                    Market = GetString(reader, 2),
                    DatabaseType = GetString(reader, 3),
                    EffectiveDateKey = GetString(reader, 4),
                    StepName = GetString(reader, 5),
                    ActionName = GetString(reader, 6),
                    LastUpdatedBy = GetString(reader, 7),
                    LastUpdatedDateTime = reader.IsDBNull(8) ? null : reader.GetDateTime(8)
                });
            }

            return new AppEngineBrowseResult
            {
                Items = items
            };
        }
        catch (Exception exception)
        {
            return new AppEngineBrowseResult
            {
                ErrorMessage = exception.Message
            };
        }
    }

    public async Task<AppEngineSourceResult> GetSourceAsync(
        OracleConnectionOptions options,
        AppEngineItem item,
        CancellationToken cancellationToken = default)
    {
        const string query = """
SELECT PROGSEQ, PCTEXT
FROM PSPCMTXT
WHERE OBJECTID1 = 66
  AND NVL(OBJECTVALUE1, ' ') = NVL(:objectValue1, ' ')
  AND NVL(OBJECTVALUE2, ' ') = NVL(:objectValue2, ' ')
  AND NVL(OBJECTVALUE3, ' ') = NVL(:objectValue3, ' ')
  AND NVL(OBJECTVALUE4, ' ') = NVL(:objectValue4, ' ')
  AND NVL(OBJECTVALUE5, ' ') = NVL(:objectValue5, ' ')
  AND NVL(OBJECTVALUE6, ' ') = NVL(:objectValue6, ' ')
  AND NVL(OBJECTVALUE7, ' ') = NVL(:objectValue7, ' ')
ORDER BY PROGSEQ
""";

        try
        {
            StringBuilder sourceBuilder = new();

            await using OracleConnection connection = new(OracleConnectionStringFactory.Create(options));
            await connection.OpenAsync(cancellationToken);

            await using OracleCommand command = new(query, connection);
            command.BindByName = true;
            command.Parameters.Add("objectValue1", OracleDbType.Varchar2, item.ProgramName, System.Data.ParameterDirection.Input);
            command.Parameters.Add("objectValue2", OracleDbType.Varchar2, item.SectionName, System.Data.ParameterDirection.Input);
            command.Parameters.Add("objectValue3", OracleDbType.Varchar2, item.Market, System.Data.ParameterDirection.Input);
            command.Parameters.Add("objectValue4", OracleDbType.Varchar2, item.DatabaseType, System.Data.ParameterDirection.Input);
            command.Parameters.Add("objectValue5", OracleDbType.Varchar2, item.EffectiveDateKey, System.Data.ParameterDirection.Input);
            command.Parameters.Add("objectValue6", OracleDbType.Varchar2, item.StepName, System.Data.ParameterDirection.Input);
            command.Parameters.Add("objectValue7", OracleDbType.Varchar2, item.ActionName, System.Data.ParameterDirection.Input);

            await using OracleDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                if (sourceBuilder.Length > 0)
                {
                    sourceBuilder.AppendLine();
                }

                sourceBuilder.Append(reader.IsDBNull(1) ? string.Empty : reader.GetString(1));
            }

            return new AppEngineSourceResult
            {
                SourceText = sourceBuilder.ToString()
            };
        }
        catch (Exception exception)
        {
            return new AppEngineSourceResult
            {
                ErrorMessage = exception.Message
            };
        }
    }

    public async Task<AppEngineSourceSearchResult> SearchSourceAsync(
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
    WHERE text_match.OBJECTID1 = 66
      AND NVL(TRIM(text_match.OBJECTVALUE1), ' ') <> ' '
      AND NVL(TRIM(text_match.OBJECTVALUE2), ' ') <> ' '
      AND NVL(TRIM(text_match.OBJECTVALUE6), ' ') <> ' '
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
    WHERE prog.OBJECTID1 = 66
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
        ON NVL(prog_rows.OBJECTVALUE1, ' ') = NVL(matched_rows.OBJECTVALUE1, ' ')
        AND NVL(prog_rows.OBJECTVALUE2, ' ') = NVL(matched_rows.OBJECTVALUE2, ' ')
        AND NVL(prog_rows.OBJECTVALUE3, ' ') = NVL(matched_rows.OBJECTVALUE3, ' ')
        AND NVL(prog_rows.OBJECTVALUE4, ' ') = NVL(matched_rows.OBJECTVALUE4, ' ')
        AND NVL(prog_rows.OBJECTVALUE5, ' ') = NVL(matched_rows.OBJECTVALUE5, ' ')
        AND NVL(prog_rows.OBJECTVALUE6, ' ') = NVL(matched_rows.OBJECTVALUE6, ' ')
        AND NVL(prog_rows.OBJECTVALUE7, ' ') = NVL(matched_rows.OBJECTVALUE7, ' ')
    WHERE matched_rows.MATCH_ROW_NUMBER = 1
    ORDER BY
        UPPER(matched_rows.OBJECTVALUE1),
        UPPER(matched_rows.OBJECTVALUE2),
        UPPER(matched_rows.OBJECTVALUE6),
        UPPER(matched_rows.OBJECTVALUE7),
        UPPER(matched_rows.OBJECTVALUE3),
        UPPER(matched_rows.OBJECTVALUE4),
        UPPER(matched_rows.OBJECTVALUE5)
) search_rows
WHERE ROWNUM <= :maxResults
""";

        try
        {
            List<AppEngineSourceSearchMatch> matches = [];

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
                AppEngineItem item = new()
                {
                    ProgramName = GetString(reader, 0),
                    SectionName = GetString(reader, 1),
                    Market = GetString(reader, 2),
                    DatabaseType = GetString(reader, 3),
                    EffectiveDateKey = GetString(reader, 4),
                    StepName = GetString(reader, 5),
                    ActionName = GetString(reader, 6),
                    LastUpdatedBy = GetString(reader, 9),
                    LastUpdatedDateTime = reader.IsDBNull(10) ? null : reader.GetDateTime(10)
                };

                matches.Add(new AppEngineSourceSearchMatch
                {
                    Item = item,
                    MatchSequence = reader.IsDBNull(7) ? 0 : Convert.ToInt32(reader.GetValue(7)),
                    MatchPreview = NormalizePreview(GetString(reader, 8))
                });
            }

            return new AppEngineSourceSearchResult
            {
                Matches = matches,
                WasLimited = matches.Count >= maxResults
            };
        }
        catch (Exception exception)
        {
            return new AppEngineSourceSearchResult
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
