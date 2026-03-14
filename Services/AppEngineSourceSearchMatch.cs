using System.Collections.Generic;
using System.Linq;
using PeopleCodeIDECompanion.Models;

namespace PeopleCodeIDECompanion.Services;

public sealed class AppEngineSourceSearchMatch
{
    public AppEngineItem Item { get; init; } = new();

    public int MatchSequence { get; init; }

    public string MatchPreview { get; init; } = string.Empty;

    public string ItemDisplayName => Item.DisplayName;

    public string ContextSummary
    {
        get
        {
            List<string> parts =
            [
                Item.ProgramName,
                $"Section {Item.SectionName}",
                $"Step {Item.StepName}"
            ];

            if (!string.IsNullOrWhiteSpace(Item.ActionName))
            {
                parts.Add($"Action {Item.ActionName}");
            }

            if (!string.IsNullOrWhiteSpace(Item.Market))
            {
                parts.Add($"Market {Item.Market}");
            }

            if (!string.IsNullOrWhiteSpace(Item.DatabaseType))
            {
                parts.Add($"DB Type {Item.DatabaseType}");
            }

            if (!string.IsNullOrWhiteSpace(Item.EffectiveDateKey))
            {
                parts.Add($"EffDt {Item.EffectiveDateKey}");
            }

            if (MatchSequence > 0)
            {
                parts.Add($"PROGSEQ {MatchSequence}");
            }

            return string.Join(" | ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
        }
    }
}
