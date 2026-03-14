using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;
using PeopleCodeIDECompanion.Models;

namespace PeopleCodeIDECompanion.Services;

public sealed class RecordPeopleCodeBrowserService
{
    public async Task<RecordPeopleCodeBrowseResult> GetItemsAsync(
        OracleConnectionOptions options,
        CancellationToken cancellationToken = default)
    {
        const string query = """
SELECT
    text_keys.OBJECTVALUE1,
    text_keys.OBJECTVALUE2,
    text_keys.OBJECTVALUE3,
    MAX(prog.LASTUPDOPRID) AS LASTUPDOPRID,
    MAX(prog.LASTUPDDTTM) AS LASTUPDDTTM
FROM
(
    SELECT DISTINCT
        OBJECTVALUE1,
        OBJECTVALUE2,
        OBJECTVALUE3
    FROM PSPCMTXT
    WHERE OBJECTID1 = 1
      AND OBJECTID2 = 2
      AND OBJECTID3 = 12
      AND NVL(TRIM(OBJECTVALUE1), ' ') <> ' '
      AND NVL(TRIM(OBJECTVALUE2), ' ') <> ' '
      AND NVL(TRIM(OBJECTVALUE3), ' ') <> ' '
) text_keys
LEFT JOIN PSPCMPROG prog
    ON prog.OBJECTID1 = 1
    AND prog.OBJECTID2 = 2
    AND prog.OBJECTID3 = 12
    AND NVL(prog.OBJECTVALUE1, ' ') = NVL(text_keys.OBJECTVALUE1, ' ')
    AND NVL(prog.OBJECTVALUE2, ' ') = NVL(text_keys.OBJECTVALUE2, ' ')
    AND NVL(prog.OBJECTVALUE3, ' ') = NVL(text_keys.OBJECTVALUE3, ' ')
GROUP BY
    text_keys.OBJECTVALUE1,
    text_keys.OBJECTVALUE2,
    text_keys.OBJECTVALUE3
ORDER BY
    UPPER(text_keys.OBJECTVALUE1),
    UPPER(text_keys.OBJECTVALUE2),
    UPPER(text_keys.OBJECTVALUE3)
""";

        try
        {
            List<RecordPeopleCodeItem> items = [];

            await using OracleConnection connection = new(OracleConnectionStringFactory.Create(options));
            await connection.OpenAsync(cancellationToken);

            await using OracleCommand command = new(query, connection);
            await using OracleDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(new RecordPeopleCodeItem
                {
                    RecordName = GetString(reader, 0),
                    FieldName = GetString(reader, 1),
                    EventName = GetString(reader, 2),
                    LastUpdatedBy = GetString(reader, 3),
                    LastUpdatedDateTime = reader.IsDBNull(4) ? null : reader.GetDateTime(4)
                });
            }

            return new RecordPeopleCodeBrowseResult
            {
                Items = items
            };
        }
        catch (Exception exception)
        {
            return new RecordPeopleCodeBrowseResult
            {
                ErrorMessage = exception.Message
            };
        }
    }

    public async Task<RecordPeopleCodeSourceResult> GetSourceAsync(
        OracleConnectionOptions options,
        RecordPeopleCodeItem item,
        CancellationToken cancellationToken = default)
    {
        const string query = """
SELECT PROGSEQ, PCTEXT
FROM PSPCMTXT
WHERE OBJECTID1 = 1
  AND OBJECTID2 = 2
  AND OBJECTID3 = 12
  AND NVL(OBJECTVALUE1, ' ') = NVL(:recordName, ' ')
  AND NVL(OBJECTVALUE2, ' ') = NVL(:fieldName, ' ')
  AND NVL(OBJECTVALUE3, ' ') = NVL(:eventName, ' ')
ORDER BY PROGSEQ
""";

        try
        {
            StringBuilder sourceBuilder = new();

            await using OracleConnection connection = new(OracleConnectionStringFactory.Create(options));
            await connection.OpenAsync(cancellationToken);

            await using OracleCommand command = new(query, connection);
            command.BindByName = true;
            command.Parameters.Add("recordName", OracleDbType.Varchar2, item.RecordName, System.Data.ParameterDirection.Input);
            command.Parameters.Add("fieldName", OracleDbType.Varchar2, item.FieldName, System.Data.ParameterDirection.Input);
            command.Parameters.Add("eventName", OracleDbType.Varchar2, item.EventName, System.Data.ParameterDirection.Input);

            await using OracleDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                if (sourceBuilder.Length > 0)
                {
                    sourceBuilder.AppendLine();
                }

                sourceBuilder.Append(reader.IsDBNull(1) ? string.Empty : reader.GetString(1));
            }

            return new RecordPeopleCodeSourceResult
            {
                SourceText = sourceBuilder.ToString()
            };
        }
        catch (Exception exception)
        {
            return new RecordPeopleCodeSourceResult
            {
                ErrorMessage = exception.Message
            };
        }
    }

    public async Task<RecordPeopleCodeSourceSearchResult> SearchSourceAsync(
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
        text_match.PROGSEQ,
        text_match.PCTEXT,
        ROW_NUMBER() OVER
        (
            PARTITION BY
                text_match.OBJECTVALUE1,
                text_match.OBJECTVALUE2,
                text_match.OBJECTVALUE3
            ORDER BY text_match.PROGSEQ
        ) AS MATCH_ROW_NUMBER
    FROM PSPCMTXT text_match
    WHERE text_match.OBJECTID1 = 1
      AND text_match.OBJECTID2 = 2
      AND text_match.OBJECTID3 = 12
      AND NVL(TRIM(text_match.OBJECTVALUE1), ' ') <> ' '
      AND NVL(TRIM(text_match.OBJECTVALUE2), ' ') <> ' '
      AND NVL(TRIM(text_match.OBJECTVALUE3), ' ') <> ' '
      AND DBMS_LOB.INSTR(UPPER(text_match.PCTEXT), :searchTextUpper) > 0
),
prog_rows AS
(
    SELECT
        prog.OBJECTVALUE1,
        prog.OBJECTVALUE2,
        prog.OBJECTVALUE3,
        MAX(prog.LASTUPDOPRID) AS LASTUPDOPRID,
        MAX(prog.LASTUPDDTTM) AS LASTUPDDTTM
    FROM PSPCMPROG prog
    WHERE prog.OBJECTID1 = 1
      AND prog.OBJECTID2 = 2
      AND prog.OBJECTID3 = 12
    GROUP BY
        prog.OBJECTVALUE1,
        prog.OBJECTVALUE2,
        prog.OBJECTVALUE3
)
SELECT
    search_rows.OBJECTVALUE1,
    search_rows.OBJECTVALUE2,
    search_rows.OBJECTVALUE3,
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
        matched_rows.PROGSEQ AS MATCH_PROGSEQ,
        matched_rows.PCTEXT AS MATCH_PREVIEW,
        prog_rows.LASTUPDOPRID,
        prog_rows.LASTUPDDTTM
    FROM matched_rows
    LEFT JOIN prog_rows
        ON NVL(prog_rows.OBJECTVALUE1, ' ') = NVL(matched_rows.OBJECTVALUE1, ' ')
        AND NVL(prog_rows.OBJECTVALUE2, ' ') = NVL(matched_rows.OBJECTVALUE2, ' ')
        AND NVL(prog_rows.OBJECTVALUE3, ' ') = NVL(matched_rows.OBJECTVALUE3, ' ')
    WHERE matched_rows.MATCH_ROW_NUMBER = 1
    ORDER BY
        UPPER(matched_rows.OBJECTVALUE1),
        UPPER(matched_rows.OBJECTVALUE2),
        UPPER(matched_rows.OBJECTVALUE3)
) search_rows
WHERE ROWNUM <= :maxResults
""";

        try
        {
            List<RecordPeopleCodeSourceSearchMatch> matches = [];

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
                RecordPeopleCodeItem item = new()
                {
                    RecordName = GetString(reader, 0),
                    FieldName = GetString(reader, 1),
                    EventName = GetString(reader, 2),
                    LastUpdatedBy = GetString(reader, 5),
                    LastUpdatedDateTime = reader.IsDBNull(6) ? null : reader.GetDateTime(6)
                };

                matches.Add(new RecordPeopleCodeSourceSearchMatch
                {
                    Item = item,
                    MatchSequence = reader.IsDBNull(3) ? 0 : Convert.ToInt32(reader.GetValue(3)),
                    MatchPreview = NormalizePreview(GetString(reader, 4))
                });
            }

            return new RecordPeopleCodeSourceSearchResult
            {
                Matches = matches,
                WasLimited = matches.Count >= maxResults
            };
        }
        catch (Exception exception)
        {
            return new RecordPeopleCodeSourceSearchResult
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
