namespace PeopleCodeIDECompanion.Models;

public sealed class PeopleCodeAuthoritativeIdentity
{
    public int ObjectId1 { get; init; }

    public int ObjectId2 { get; init; }

    public int ObjectId3 { get; init; }

    public int ObjectId4 { get; init; }

    public int ObjectId5 { get; init; }

    public int ObjectId6 { get; init; }

    public int ObjectId7 { get; init; }

    public string ObjectValue1 { get; init; } = string.Empty;

    public string ObjectValue2 { get; init; } = string.Empty;

    public string ObjectValue3 { get; init; } = string.Empty;

    public string ObjectValue4 { get; init; } = string.Empty;

    public string ObjectValue5 { get; init; } = string.Empty;

    public string ObjectValue6 { get; init; } = string.Empty;

    public string ObjectValue7 { get; init; } = string.Empty;

    public string AuthoritativeStoreName { get; init; } = string.Empty;

    public string SourceProjectionStoreName { get; init; } = string.Empty;

    public int ProgramRowCount { get; init; }

    public int ProgramMinSequence { get; init; }

    public int ProgramMaxSequence { get; init; }

    public int TextRowCount { get; init; }

    public int TextMinSequence { get; init; }

    public int TextMaxSequence { get; init; }
}
