using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;
using PeopleCodeIDECompanion.Models;

namespace PeopleCodeIDECompanion.Services;

public sealed class PagePeopleCodeBrowserService
{
    public async Task<PagePeopleCodeBrowseResult> GetItemsAsync(
        OracleConnectionOptions options,
        CancellationToken cancellationToken = default)
    {
        const string query = """
SELECT
    text_keys.OBJECTID2,
    text_keys.OBJECTID3,
    text_keys.OBJECTID4,
    text_keys.OBJECTID5,
    text_keys.OBJECTID6,
    text_keys.OBJECTID7,
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
        OBJECTID2,
        OBJECTID3,
        OBJECTID4,
        OBJECTID5,
        OBJECTID6,
        OBJECTID7,
        OBJECTVALUE1,
        OBJECTVALUE2,
        OBJECTVALUE3,
        OBJECTVALUE4,
        OBJECTVALUE5,
        OBJECTVALUE6,
        OBJECTVALUE7
    FROM PSPCMTXT
    WHERE OBJECTID1 = 9
      AND NVL(TRIM(OBJECTVALUE1), ' ') <> ' '
) text_keys
LEFT JOIN PSPCMPROG prog
    ON prog.OBJECTID1 = 9
    AND NVL(prog.OBJECTID2, -1) = NVL(text_keys.OBJECTID2, -1)
    AND NVL(prog.OBJECTID3, -1) = NVL(text_keys.OBJECTID3, -1)
    AND NVL(prog.OBJECTID4, -1) = NVL(text_keys.OBJECTID4, -1)
    AND NVL(prog.OBJECTID5, -1) = NVL(text_keys.OBJECTID5, -1)
    AND NVL(prog.OBJECTID6, -1) = NVL(text_keys.OBJECTID6, -1)
    AND NVL(prog.OBJECTID7, -1) = NVL(text_keys.OBJECTID7, -1)
    AND NVL(prog.OBJECTVALUE1, ' ') = NVL(text_keys.OBJECTVALUE1, ' ')
    AND NVL(prog.OBJECTVALUE2, ' ') = NVL(text_keys.OBJECTVALUE2, ' ')
    AND NVL(prog.OBJECTVALUE3, ' ') = NVL(text_keys.OBJECTVALUE3, ' ')
    AND NVL(prog.OBJECTVALUE4, ' ') = NVL(text_keys.OBJECTVALUE4, ' ')
    AND NVL(prog.OBJECTVALUE5, ' ') = NVL(text_keys.OBJECTVALUE5, ' ')
    AND NVL(prog.OBJECTVALUE6, ' ') = NVL(text_keys.OBJECTVALUE6, ' ')
    AND NVL(prog.OBJECTVALUE7, ' ') = NVL(text_keys.OBJECTVALUE7, ' ')
GROUP BY
    text_keys.OBJECTID2,
    text_keys.OBJECTID3,
    text_keys.OBJECTID4,
    text_keys.OBJECTID5,
    text_keys.OBJECTID6,
    text_keys.OBJECTID7,
    text_keys.OBJECTVALUE1,
    text_keys.OBJECTVALUE2,
    text_keys.OBJECTVALUE3,
    text_keys.OBJECTVALUE4,
    text_keys.OBJECTVALUE5,
    text_keys.OBJECTVALUE6,
    text_keys.OBJECTVALUE7
ORDER BY
    UPPER(text_keys.OBJECTVALUE1),
    NVL(text_keys.OBJECTID2, -1),
    NVL(text_keys.OBJECTID3, -1),
    NVL(text_keys.OBJECTID4, -1),
    UPPER(text_keys.OBJECTVALUE2),
    UPPER(text_keys.OBJECTVALUE3),
    UPPER(text_keys.OBJECTVALUE4),
    UPPER(text_keys.OBJECTVALUE5),
    UPPER(text_keys.OBJECTVALUE6),
    UPPER(text_keys.OBJECTVALUE7)
""";

        try
        {
            List<PagePeopleCodeItem> items = [];

            await using OracleConnection connection = new(OracleConnectionStringFactory.Create(options));
            await connection.OpenAsync(cancellationToken);

            await using OracleCommand command = new(query, connection);
            await using OracleDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(ReadItem(reader));
            }

            return new PagePeopleCodeBrowseResult
            {
                Items = items
            };
        }
        catch (Exception exception)
        {
            return new PagePeopleCodeBrowseResult
            {
                ErrorMessage = exception.Message
            };
        }
    }

    public async Task<PagePeopleCodeSourceResult> GetSourceAsync(
        OracleConnectionOptions options,
        PagePeopleCodeItem item,
        CancellationToken cancellationToken = default)
    {
        const string query = """
SELECT PROGSEQ, PCTEXT
FROM PSPCMTXT
WHERE OBJECTID1 = 9
  AND NVL(OBJECTID2, -1) = NVL(:objectId2, -1)
  AND NVL(OBJECTID3, -1) = NVL(:objectId3, -1)
  AND NVL(OBJECTID4, -1) = NVL(:objectId4, -1)
  AND NVL(OBJECTID5, -1) = NVL(:objectId5, -1)
  AND NVL(OBJECTID6, -1) = NVL(:objectId6, -1)
  AND NVL(OBJECTID7, -1) = NVL(:objectId7, -1)
  AND NVL(OBJECTVALUE1, ' ') = NVL(:pageName, ' ')
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
            AddNullableIntParameter(command, "objectId2", item.ObjectId2);
            AddNullableIntParameter(command, "objectId3", item.ObjectId3);
            AddNullableIntParameter(command, "objectId4", item.ObjectId4);
            AddNullableIntParameter(command, "objectId5", item.ObjectId5);
            AddNullableIntParameter(command, "objectId6", item.ObjectId6);
            AddNullableIntParameter(command, "objectId7", item.ObjectId7);
            command.Parameters.Add("pageName", OracleDbType.Varchar2, item.PageName, System.Data.ParameterDirection.Input);
            command.Parameters.Add("objectValue2", OracleDbType.Varchar2, item.ObjectValue2, System.Data.ParameterDirection.Input);
            command.Parameters.Add("objectValue3", OracleDbType.Varchar2, item.ObjectValue3, System.Data.ParameterDirection.Input);
            command.Parameters.Add("objectValue4", OracleDbType.Varchar2, item.ObjectValue4, System.Data.ParameterDirection.Input);
            command.Parameters.Add("objectValue5", OracleDbType.Varchar2, item.ObjectValue5, System.Data.ParameterDirection.Input);
            command.Parameters.Add("objectValue6", OracleDbType.Varchar2, item.ObjectValue6, System.Data.ParameterDirection.Input);
            command.Parameters.Add("objectValue7", OracleDbType.Varchar2, item.ObjectValue7, System.Data.ParameterDirection.Input);

            await using OracleDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                if (sourceBuilder.Length > 0)
                {
                    sourceBuilder.AppendLine();
                }

                sourceBuilder.Append(reader.IsDBNull(1) ? string.Empty : reader.GetString(1));
            }

            return new PagePeopleCodeSourceResult
            {
                SourceText = sourceBuilder.ToString()
            };
        }
        catch (Exception exception)
        {
            return new PagePeopleCodeSourceResult
            {
                ErrorMessage = exception.Message
            };
        }
    }

    public async Task<PagePeopleCodeSourceSearchResult> SearchSourceAsync(
        OracleConnectionOptions options,
        string searchText,
        int maxResults,
        CancellationToken cancellationToken = default)
    {
        const string query = """
WITH matched_rows AS
(
    SELECT
        text_match.OBJECTID2,
        text_match.OBJECTID3,
        text_match.OBJECTID4,
        text_match.OBJECTID5,
        text_match.OBJECTID6,
        text_match.OBJECTID7,
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
                text_match.OBJECTID2,
                text_match.OBJECTID3,
                text_match.OBJECTID4,
                text_match.OBJECTID5,
                text_match.OBJECTID6,
                text_match.OBJECTID7,
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
    WHERE text_match.OBJECTID1 = 9
      AND NVL(TRIM(text_match.OBJECTVALUE1), ' ') <> ' '
      AND DBMS_LOB.INSTR(UPPER(text_match.PCTEXT), :searchTextUpper) > 0
),
prog_rows AS
(
    SELECT
        prog.OBJECTID2,
        prog.OBJECTID3,
        prog.OBJECTID4,
        prog.OBJECTID5,
        prog.OBJECTID6,
        prog.OBJECTID7,
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
    WHERE prog.OBJECTID1 = 9
    GROUP BY
        prog.OBJECTID2,
        prog.OBJECTID3,
        prog.OBJECTID4,
        prog.OBJECTID5,
        prog.OBJECTID6,
        prog.OBJECTID7,
        prog.OBJECTVALUE1,
        prog.OBJECTVALUE2,
        prog.OBJECTVALUE3,
        prog.OBJECTVALUE4,
        prog.OBJECTVALUE5,
        prog.OBJECTVALUE6,
        prog.OBJECTVALUE7
)
SELECT
    search_rows.OBJECTID2,
    search_rows.OBJECTID3,
    search_rows.OBJECTID4,
    search_rows.OBJECTID5,
    search_rows.OBJECTID6,
    search_rows.OBJECTID7,
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
        matched_rows.OBJECTID2,
        matched_rows.OBJECTID3,
        matched_rows.OBJECTID4,
        matched_rows.OBJECTID5,
        matched_rows.OBJECTID6,
        matched_rows.OBJECTID7,
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
        ON NVL(prog_rows.OBJECTID2, -1) = NVL(matched_rows.OBJECTID2, -1)
        AND NVL(prog_rows.OBJECTID3, -1) = NVL(matched_rows.OBJECTID3, -1)
        AND NVL(prog_rows.OBJECTID4, -1) = NVL(matched_rows.OBJECTID4, -1)
        AND NVL(prog_rows.OBJECTID5, -1) = NVL(matched_rows.OBJECTID5, -1)
        AND NVL(prog_rows.OBJECTID6, -1) = NVL(matched_rows.OBJECTID6, -1)
        AND NVL(prog_rows.OBJECTID7, -1) = NVL(matched_rows.OBJECTID7, -1)
        AND NVL(prog_rows.OBJECTVALUE1, ' ') = NVL(matched_rows.OBJECTVALUE1, ' ')
        AND NVL(prog_rows.OBJECTVALUE2, ' ') = NVL(matched_rows.OBJECTVALUE2, ' ')
        AND NVL(prog_rows.OBJECTVALUE3, ' ') = NVL(matched_rows.OBJECTVALUE3, ' ')
        AND NVL(prog_rows.OBJECTVALUE4, ' ') = NVL(matched_rows.OBJECTVALUE4, ' ')
        AND NVL(prog_rows.OBJECTVALUE5, ' ') = NVL(matched_rows.OBJECTVALUE5, ' ')
        AND NVL(prog_rows.OBJECTVALUE6, ' ') = NVL(matched_rows.OBJECTVALUE6, ' ')
        AND NVL(prog_rows.OBJECTVALUE7, ' ') = NVL(matched_rows.OBJECTVALUE7, ' ')
    WHERE matched_rows.MATCH_ROW_NUMBER = 1
    ORDER BY
        UPPER(matched_rows.OBJECTVALUE1),
        NVL(matched_rows.OBJECTID2, -1),
        NVL(matched_rows.OBJECTID3, -1),
        NVL(matched_rows.OBJECTID4, -1),
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
            List<PagePeopleCodeSourceSearchMatch> matches = [];

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
                PagePeopleCodeItem item = ReadItem(reader, 0, 6, 15, 16);

                matches.Add(new PagePeopleCodeSourceSearchMatch
                {
                    Item = item,
                    MatchSequence = reader.IsDBNull(13) ? 0 : Convert.ToInt32(reader.GetValue(13)),
                    MatchPreview = NormalizePreview(GetString(reader, 14))
                });
            }

            return new PagePeopleCodeSourceSearchResult
            {
                Matches = matches,
                WasLimited = matches.Count >= maxResults
            };
        }
        catch (Exception exception)
        {
            return new PagePeopleCodeSourceSearchResult
            {
                ErrorMessage = exception.Message
            };
        }
    }

    private static PagePeopleCodeItem ReadItem(
        OracleDataReader reader,
        int objectIdStartIndex = 0,
        int pageNameIndex = 6,
        int lastUpdatedByIndex = 13,
        int lastUpdatedDateTimeIndex = 14)
    {
        return new PagePeopleCodeItem
        {
            ObjectId2 = GetNullableInt(reader, objectIdStartIndex),
            ObjectId3 = GetNullableInt(reader, objectIdStartIndex + 1),
            ObjectId4 = GetNullableInt(reader, objectIdStartIndex + 2),
            ObjectId5 = GetNullableInt(reader, objectIdStartIndex + 3),
            ObjectId6 = GetNullableInt(reader, objectIdStartIndex + 4),
            ObjectId7 = GetNullableInt(reader, objectIdStartIndex + 5),
            PageName = GetString(reader, pageNameIndex),
            ObjectValue2 = GetString(reader, pageNameIndex + 1),
            ObjectValue3 = GetString(reader, pageNameIndex + 2),
            ObjectValue4 = GetString(reader, pageNameIndex + 3),
            ObjectValue5 = GetString(reader, pageNameIndex + 4),
            ObjectValue6 = GetString(reader, pageNameIndex + 5),
            ObjectValue7 = GetString(reader, pageNameIndex + 6),
            LastUpdatedBy = GetString(reader, lastUpdatedByIndex),
            LastUpdatedDateTime = reader.IsDBNull(lastUpdatedDateTimeIndex) ? null : reader.GetDateTime(lastUpdatedDateTimeIndex)
        };
    }

    private static void AddNullableIntParameter(OracleCommand command, string parameterName, int? value)
    {
        OracleParameter parameter = command.Parameters.Add(parameterName, OracleDbType.Int32);
        parameter.Value = value ?? (object)DBNull.Value;
    }

    private static string GetString(OracleDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
    }

    private static int? GetNullableInt(OracleDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? null : Convert.ToInt32(reader.GetValue(ordinal));
    }

    private static string NormalizePreview(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "(matching source row was blank)"
            : value.Trim().Replace('\t', ' ');
    }
}
