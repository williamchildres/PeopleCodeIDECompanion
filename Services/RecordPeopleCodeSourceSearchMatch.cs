using System.Collections.Generic;
using System.Linq;
using PeopleCodeIDECompanion.Models;

namespace PeopleCodeIDECompanion.Services;

public sealed class RecordPeopleCodeSourceSearchMatch
{
    public RecordPeopleCodeItem Item { get; init; } = new();

    public int MatchSequence { get; init; }

    public string MatchPreview { get; init; } = string.Empty;

    public string ItemDisplayName => Item.DisplayName;

    public string ContextSummary
    {
        get
        {
            List<string> parts =
            [
                Item.RecordName,
                string.IsNullOrWhiteSpace(Item.FieldName) ? "Record Event" : $"Field {Item.FieldName}",
                $"Event {Item.EventName}"
            ];

            if (MatchSequence > 0)
            {
                parts.Add($"PROGSEQ {MatchSequence}");
            }

            return string.Join(" | ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
        }
    }
}
