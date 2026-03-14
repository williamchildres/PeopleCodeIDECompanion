using System.Collections.Generic;

namespace PeopleCodeIDECompanion.Models;

public sealed class PeopleCodeComparePaneViewModel
{
    public string ProfileDisplayName { get; init; } = string.Empty;

    public string ProfileContext { get; init; } = string.Empty;

    public string ObjectType { get; init; } = string.Empty;

    public string ObjectTitle { get; init; } = string.Empty;

    public string ObjectSubtitle { get; init; } = string.Empty;

    public string MetadataSummary { get; init; } = string.Empty;

    public string SourceText { get; init; } = string.Empty;

    public string DisplaySourceText { get; init; } = string.Empty;

    public string DisplayLineNumbers { get; init; } = string.Empty;

    public IReadOnlyList<CharacterRange> AddedRanges { get; init; } = System.Array.Empty<CharacterRange>();

    public IReadOnlyList<CharacterRange> RemovedRanges { get; init; } = System.Array.Empty<CharacterRange>();

    public IReadOnlyList<CharacterRange> ChangedRanges { get; init; } = System.Array.Empty<CharacterRange>();

    public string StatusMessage { get; init; } = string.Empty;

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);
}
