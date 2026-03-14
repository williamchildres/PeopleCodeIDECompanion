using System.Collections.Generic;

namespace PeopleCodeIDECompanion.Models;

public sealed class PeopleCodeRepeatedCodeBlock
{
    public string NormalizedText { get; init; } = string.Empty;

    public string SnippetPreview { get; init; } = string.Empty;

    public IReadOnlyList<PeopleCodeRepeatedCodeOccurrence> Occurrences { get; init; } = [];

    public int OccurrenceCount => Occurrences.Count;

    public string Summary => $"{OccurrenceCount} occurrence(s)";
}
