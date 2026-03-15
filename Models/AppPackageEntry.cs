using System;
using System.Collections.Generic;
using System.Linq;

namespace PeopleCodeIDECompanion.Models;

public sealed class AppPackageEntry
{
    public int ObjectId1 { get; init; } = 104;

    public int ObjectId2 { get; init; }

    public int ObjectId3 { get; init; }

    public int ObjectId4 { get; init; }

    public int ObjectId5 { get; init; }

    public int ObjectId6 { get; init; }

    public int ObjectId7 { get; init; }

    public string PackageRoot { get; init; } = string.Empty;

    public string ObjectValue2 { get; init; } = string.Empty;

    public string ObjectValue3 { get; init; } = string.Empty;

    public string ObjectValue4 { get; init; } = string.Empty;

    public string ObjectValue5 { get; init; } = string.Empty;

    public string ObjectValue6 { get; init; } = string.Empty;

    public string ObjectValue7 { get; init; } = string.Empty;

    public string LastUpdatedBy { get; init; } = string.Empty;

    public DateTime? LastUpdatedDateTime { get; init; }

    public int TextRowCount { get; init; }

    public int TextMinSequence { get; init; }

    public int TextMaxSequence { get; init; }

    public int ProgramRowCount { get; init; }

    public int ProgramMinSequence { get; init; }

    public int ProgramMaxSequence { get; init; }

    public string AuthoritativeStoreName => "PSPCMPROG";

    public string SourceProjectionStoreName => "PSPCMTXT";

    public PeopleCodeAuthoritativeIdentity AuthoritativeIdentity =>
        new()
        {
            ObjectId1 = ObjectId1,
            ObjectId2 = ObjectId2,
            ObjectId3 = ObjectId3,
            ObjectId4 = ObjectId4,
            ObjectId5 = ObjectId5,
            ObjectId6 = ObjectId6,
            ObjectId7 = ObjectId7,
            ObjectValue1 = PackageRoot,
            ObjectValue2 = ObjectValue2,
            ObjectValue3 = ObjectValue3,
            ObjectValue4 = ObjectValue4,
            ObjectValue5 = ObjectValue5,
            ObjectValue6 = ObjectValue6,
            ObjectValue7 = ObjectValue7,
            AuthoritativeStoreName = AuthoritativeStoreName,
            SourceProjectionStoreName = SourceProjectionStoreName,
            ProgramRowCount = ProgramRowCount,
            ProgramMinSequence = ProgramMinSequence,
            ProgramMaxSequence = ProgramMaxSequence,
            TextRowCount = TextRowCount,
            TextMinSequence = TextMinSequence,
            TextMaxSequence = TextMaxSequence
        };

    public string DisplayName
    {
        get
        {
            List<string> segments = new()
            {
                PackageRoot,
                ObjectValue2,
                ObjectValue3,
                ObjectValue4,
                ObjectValue5,
                ObjectValue6,
                ObjectValue7
            };

            return string.Join(":", segments.Where(segment => !string.IsNullOrWhiteSpace(segment)));
        }
    }

    public string EntryType
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(ObjectValue4) ||
                !string.IsNullOrWhiteSpace(ObjectValue7) ||
                !string.IsNullOrWhiteSpace(ObjectValue6) ||
                !string.IsNullOrWhiteSpace(ObjectValue5))
            {
                return "Program Entry";
            }

            if (!string.IsNullOrWhiteSpace(ObjectValue4) || !string.IsNullOrWhiteSpace(ObjectValue3))
            {
                return "Class Path";
            }

            if (!string.IsNullOrWhiteSpace(ObjectValue2))
            {
                return "Package Path";
            }

            return "Package Root";
        }
    }
}
