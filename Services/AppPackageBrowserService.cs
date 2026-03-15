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
WITH text_keys AS
(
    SELECT
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
        OBJECTVALUE7,
        COUNT(*) AS TEXT_ROW_COUNT,
        MIN(PROGSEQ) AS TEXT_MIN_PROGSEQ,
        MAX(PROGSEQ) AS TEXT_MAX_PROGSEQ
    FROM PSPCMTXT
    WHERE OBJECTID1 = 104
    GROUP BY
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
),
prog_rows AS
(
    SELECT
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
        OBJECTVALUE7,
        COUNT(*) AS PROGRAM_ROW_COUNT,
        MIN(PROGSEQ) AS PROGRAM_MIN_PROGSEQ,
        MAX(PROGSEQ) AS PROGRAM_MAX_PROGSEQ,
        MAX(LASTUPDOPRID) AS LASTUPDOPRID,
        MAX(LASTUPDDTTM) AS LASTUPDDTTM
    FROM PSPCMPROG
    WHERE OBJECTID1 = 104
    GROUP BY
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
)
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
    text_keys.TEXT_ROW_COUNT,
    text_keys.TEXT_MIN_PROGSEQ,
    text_keys.TEXT_MAX_PROGSEQ,
    NVL(prog_rows.PROGRAM_ROW_COUNT, 0) AS PROGRAM_ROW_COUNT,
    NVL(prog_rows.PROGRAM_MIN_PROGSEQ, 0) AS PROGRAM_MIN_PROGSEQ,
    NVL(prog_rows.PROGRAM_MAX_PROGSEQ, 0) AS PROGRAM_MAX_PROGSEQ,
    prog_rows.LASTUPDOPRID,
    prog_rows.LASTUPDDTTM
FROM text_keys
LEFT JOIN prog_rows
    ON prog_rows.OBJECTID2 = text_keys.OBJECTID2
    AND prog_rows.OBJECTID3 = text_keys.OBJECTID3
    AND prog_rows.OBJECTID4 = text_keys.OBJECTID4
    AND prog_rows.OBJECTID5 = text_keys.OBJECTID5
    AND prog_rows.OBJECTID6 = text_keys.OBJECTID6
    AND prog_rows.OBJECTID7 = text_keys.OBJECTID7
    AND prog_rows.OBJECTVALUE1 = text_keys.OBJECTVALUE1
    AND prog_rows.OBJECTVALUE2 = text_keys.OBJECTVALUE2
    AND prog_rows.OBJECTVALUE3 = text_keys.OBJECTVALUE3
    AND prog_rows.OBJECTVALUE4 = text_keys.OBJECTVALUE4
    AND prog_rows.OBJECTVALUE5 = text_keys.OBJECTVALUE5
    AND prog_rows.OBJECTVALUE6 = text_keys.OBJECTVALUE6
    AND prog_rows.OBJECTVALUE7 = text_keys.OBJECTVALUE7
ORDER BY
    NVL(text_keys.OBJECTID2, -1),
    NVL(text_keys.OBJECTID3, -1),
    NVL(text_keys.OBJECTID4, -1),
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
                entries.Add(ReadEntry(reader));
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
  AND OBJECTID2 = :objectId2
  AND OBJECTID3 = :objectId3
  AND OBJECTID4 = :objectId4
  AND OBJECTID5 = :objectId5
  AND OBJECTID6 = :objectId6
  AND OBJECTID7 = :objectId7
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
            AddEntryParameters(command, entry);

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
    WHERE text_match.OBJECTID1 = 104
      AND DBMS_LOB.INSTR(UPPER(text_match.PCTEXT), :searchTextUpper) > 0
),
text_rows AS
(
    SELECT
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
        OBJECTVALUE7,
        COUNT(*) AS TEXT_ROW_COUNT,
        MIN(PROGSEQ) AS TEXT_MIN_PROGSEQ,
        MAX(PROGSEQ) AS TEXT_MAX_PROGSEQ
    FROM PSPCMTXT
    WHERE OBJECTID1 = 104
    GROUP BY
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
        COUNT(*) AS PROGRAM_ROW_COUNT,
        MIN(prog.PROGSEQ) AS PROGRAM_MIN_PROGSEQ,
        MAX(prog.PROGSEQ) AS PROGRAM_MAX_PROGSEQ,
        MAX(prog.LASTUPDOPRID) AS LASTUPDOPRID,
        MAX(prog.LASTUPDDTTM) AS LASTUPDDTTM
    FROM PSPCMPROG prog
    WHERE prog.OBJECTID1 = 104
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
    search_rows.TEXT_ROW_COUNT,
    search_rows.TEXT_MIN_PROGSEQ,
    search_rows.TEXT_MAX_PROGSEQ,
    search_rows.PROGRAM_ROW_COUNT,
    search_rows.PROGRAM_MIN_PROGSEQ,
    search_rows.PROGRAM_MAX_PROGSEQ,
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
        text_rows.TEXT_ROW_COUNT,
        text_rows.TEXT_MIN_PROGSEQ,
        text_rows.TEXT_MAX_PROGSEQ,
        prog_rows.PROGRAM_ROW_COUNT,
        prog_rows.PROGRAM_MIN_PROGSEQ,
        prog_rows.PROGRAM_MAX_PROGSEQ,
        prog_rows.LASTUPDOPRID,
        prog_rows.LASTUPDDTTM
    FROM matched_rows
    LEFT JOIN text_rows
        ON text_rows.OBJECTID2 = matched_rows.OBJECTID2
        AND text_rows.OBJECTID3 = matched_rows.OBJECTID3
        AND text_rows.OBJECTID4 = matched_rows.OBJECTID4
        AND text_rows.OBJECTID5 = matched_rows.OBJECTID5
        AND text_rows.OBJECTID6 = matched_rows.OBJECTID6
        AND text_rows.OBJECTID7 = matched_rows.OBJECTID7
        AND text_rows.OBJECTVALUE1 = matched_rows.OBJECTVALUE1
        AND text_rows.OBJECTVALUE2 = matched_rows.OBJECTVALUE2
        AND text_rows.OBJECTVALUE3 = matched_rows.OBJECTVALUE3
        AND text_rows.OBJECTVALUE4 = matched_rows.OBJECTVALUE4
        AND text_rows.OBJECTVALUE5 = matched_rows.OBJECTVALUE5
        AND text_rows.OBJECTVALUE6 = matched_rows.OBJECTVALUE6
        AND text_rows.OBJECTVALUE7 = matched_rows.OBJECTVALUE7
    LEFT JOIN prog_rows
        ON prog_rows.OBJECTID2 = matched_rows.OBJECTID2
        AND prog_rows.OBJECTID3 = matched_rows.OBJECTID3
        AND prog_rows.OBJECTID4 = matched_rows.OBJECTID4
        AND prog_rows.OBJECTID5 = matched_rows.OBJECTID5
        AND prog_rows.OBJECTID6 = matched_rows.OBJECTID6
        AND prog_rows.OBJECTID7 = matched_rows.OBJECTID7
        AND prog_rows.OBJECTVALUE1 = matched_rows.OBJECTVALUE1
        AND prog_rows.OBJECTVALUE2 = matched_rows.OBJECTVALUE2
        AND prog_rows.OBJECTVALUE3 = matched_rows.OBJECTVALUE3
        AND prog_rows.OBJECTVALUE4 = matched_rows.OBJECTVALUE4
        AND prog_rows.OBJECTVALUE5 = matched_rows.OBJECTVALUE5
        AND prog_rows.OBJECTVALUE6 = matched_rows.OBJECTVALUE6
        AND prog_rows.OBJECTVALUE7 = matched_rows.OBJECTVALUE7
    WHERE matched_rows.MATCH_ROW_NUMBER = 1
    ORDER BY
        NVL(matched_rows.OBJECTID2, -1),
        NVL(matched_rows.OBJECTID3, -1),
        NVL(matched_rows.OBJECTID4, -1),
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
                    ObjectId2 = GetInt32(reader, 0),
                    ObjectId3 = GetInt32(reader, 1),
                    ObjectId4 = GetInt32(reader, 2),
                    ObjectId5 = GetInt32(reader, 3),
                    ObjectId6 = GetInt32(reader, 4),
                    ObjectId7 = GetInt32(reader, 5),
                    PackageRoot = GetString(reader, 6),
                    ObjectValue2 = GetString(reader, 7),
                    ObjectValue3 = GetString(reader, 8),
                    ObjectValue4 = GetString(reader, 9),
                    ObjectValue5 = GetString(reader, 10),
                    ObjectValue6 = GetString(reader, 11),
                    ObjectValue7 = GetString(reader, 12),
                    TextRowCount = GetInt32(reader, 15),
                    TextMinSequence = GetInt32(reader, 16),
                    TextMaxSequence = GetInt32(reader, 17),
                    ProgramRowCount = GetInt32(reader, 18),
                    ProgramMinSequence = GetInt32(reader, 19),
                    ProgramMaxSequence = GetInt32(reader, 20),
                    LastUpdatedBy = GetString(reader, 21),
                    LastUpdatedDateTime = reader.IsDBNull(22) ? null : reader.GetDateTime(22)
                };

                matches.Add(new AppPackageSourceSearchMatch
                {
                    Entry = entry,
                    MatchSequence = GetInt32(reader, 13),
                    MatchPreview = NormalizePreview(GetString(reader, 14))
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

    private static AppPackageEntry ReadEntry(OracleDataReader reader)
    {
        return new AppPackageEntry
        {
            ObjectId2 = GetInt32(reader, 0),
            ObjectId3 = GetInt32(reader, 1),
            ObjectId4 = GetInt32(reader, 2),
            ObjectId5 = GetInt32(reader, 3),
            ObjectId6 = GetInt32(reader, 4),
            ObjectId7 = GetInt32(reader, 5),
            PackageRoot = GetString(reader, 6),
            ObjectValue2 = GetString(reader, 7),
            ObjectValue3 = GetString(reader, 8),
            ObjectValue4 = GetString(reader, 9),
            ObjectValue5 = GetString(reader, 10),
            ObjectValue6 = GetString(reader, 11),
            ObjectValue7 = GetString(reader, 12),
            TextRowCount = GetInt32(reader, 13),
            TextMinSequence = GetInt32(reader, 14),
            TextMaxSequence = GetInt32(reader, 15),
            ProgramRowCount = GetInt32(reader, 16),
            ProgramMinSequence = GetInt32(reader, 17),
            ProgramMaxSequence = GetInt32(reader, 18),
            LastUpdatedBy = GetString(reader, 19),
            LastUpdatedDateTime = reader.IsDBNull(20) ? null : reader.GetDateTime(20)
        };
    }

    private static void AddEntryParameters(OracleCommand command, AppPackageEntry entry)
    {
        command.BindByName = true;
        command.Parameters.Add("objectId2", OracleDbType.Int32, entry.ObjectId2, System.Data.ParameterDirection.Input);
        command.Parameters.Add("objectId3", OracleDbType.Int32, entry.ObjectId3, System.Data.ParameterDirection.Input);
        command.Parameters.Add("objectId4", OracleDbType.Int32, entry.ObjectId4, System.Data.ParameterDirection.Input);
        command.Parameters.Add("objectId5", OracleDbType.Int32, entry.ObjectId5, System.Data.ParameterDirection.Input);
        command.Parameters.Add("objectId6", OracleDbType.Int32, entry.ObjectId6, System.Data.ParameterDirection.Input);
        command.Parameters.Add("objectId7", OracleDbType.Int32, entry.ObjectId7, System.Data.ParameterDirection.Input);
        command.Parameters.Add("objectValue1", OracleDbType.Varchar2, entry.PackageRoot, System.Data.ParameterDirection.Input);
        command.Parameters.Add("objectValue2", OracleDbType.Varchar2, entry.ObjectValue2, System.Data.ParameterDirection.Input);
        command.Parameters.Add("objectValue3", OracleDbType.Varchar2, entry.ObjectValue3, System.Data.ParameterDirection.Input);
        command.Parameters.Add("objectValue4", OracleDbType.Varchar2, entry.ObjectValue4, System.Data.ParameterDirection.Input);
        command.Parameters.Add("objectValue5", OracleDbType.Varchar2, entry.ObjectValue5, System.Data.ParameterDirection.Input);
        command.Parameters.Add("objectValue6", OracleDbType.Varchar2, entry.ObjectValue6, System.Data.ParameterDirection.Input);
        command.Parameters.Add("objectValue7", OracleDbType.Varchar2, entry.ObjectValue7, System.Data.ParameterDirection.Input);
    }

    private static string GetString(OracleDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
    }

    private static int GetInt32(OracleDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? 0 : Convert.ToInt32(reader.GetValue(ordinal));
    }

    private static string NormalizePreview(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "(matching source row was blank)"
            : value.Trim().Replace('\t', ' ');
    }
}
