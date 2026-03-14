using System.Collections.Generic;
using System.Linq;
using PeopleCodeIDECompanion.Models;

namespace PeopleCodeIDECompanion.Services;

public sealed class AppPackageSourceSearchMatch
{
    public AppPackageEntry Entry { get; init; } = new();

    public int MatchSequence { get; init; }

    public string MatchPreview { get; init; } = string.Empty;

    public string EntryDisplayName => Entry.DisplayName;

    public string ContextSummary
    {
        get
        {
            List<string> parts = new()
            {
                Entry.PackageRoot,
                Entry.EntryType
            };

            string classPath = string.Join(":",
                new[] { Entry.ObjectValue2, Entry.ObjectValue3, Entry.ObjectValue4 }
                    .Where(value => !string.IsNullOrWhiteSpace(value)));
            if (!string.IsNullOrWhiteSpace(classPath))
            {
                parts.Add($"Class {classPath}");
            }

            string eventOrProgram = FirstNonBlank(Entry.ObjectValue7, Entry.ObjectValue6, Entry.ObjectValue5);
            if (!string.IsNullOrWhiteSpace(eventOrProgram))
            {
                parts.Add($"Program {eventOrProgram}");
            }

            if (MatchSequence > 0)
            {
                parts.Add($"PROGSEQ {MatchSequence}");
            }

            return string.Join(" | ", parts.Where(value => !string.IsNullOrWhiteSpace(value)));
        }
    }

    private static string FirstNonBlank(params string[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }
}
