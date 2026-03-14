using System.Collections.Generic;
using System.Linq;

namespace PeopleCodeIDECompanion.Models;

public sealed class ComponentPeopleCodeComponentKey
{
    public string ComponentName { get; init; } = string.Empty;

    public string Market { get; init; } = string.Empty;

    public string DisplayName => string.IsNullOrWhiteSpace(ComponentName) ? "(blank component)" : ComponentName;

    public string SecondaryLabel => string.IsNullOrWhiteSpace(Market) ? "Market (blank)" : $"Market {Market}";

    public string SearchSummary
    {
        get
        {
            List<string> parts =
            [
                ComponentName,
                Market,
                SecondaryLabel
            ];

            return string.Join(" | ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
        }
    }
}
