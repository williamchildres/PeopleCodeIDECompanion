using System;
using System.Collections.Generic;
using System.Linq;

namespace PeopleCodeIDECompanion.Models;

public sealed class AppEngineItem
{
    public string ProgramName { get; init; } = string.Empty;

    public string SectionName { get; init; } = string.Empty;

    public string Market { get; init; } = string.Empty;

    public string DatabaseType { get; init; } = string.Empty;

    public string EffectiveDateKey { get; init; } = string.Empty;

    public string StepName { get; init; } = string.Empty;

    public string ActionName { get; init; } = string.Empty;

    public string LastUpdatedBy { get; init; } = string.Empty;

    public DateTime? LastUpdatedDateTime { get; init; }

    public string DisplayName
    {
        get
        {
            List<string> parts =
            [
                SectionName,
                StepName
            ];

            if (!string.IsNullOrWhiteSpace(ActionName))
            {
                parts.Add(ActionName);
            }

            return string.Join(" / ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
        }
    }

    public string SearchSummary
    {
        get
        {
            List<string> parts =
            [
                ProgramName,
                SectionName,
                StepName,
                ActionName,
                Market,
                DatabaseType,
                EffectiveDateKey
            ];

            return string.Join(" | ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
        }
    }

    public string StepActionLabel =>
        string.IsNullOrWhiteSpace(ActionName)
            ? StepName
            : $"{StepName} / {ActionName}";
}
