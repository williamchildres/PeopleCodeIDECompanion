using System;
using System.Collections.Generic;
using System.Linq;

namespace PeopleCodeIDECompanion.Models;

public sealed class RecordPeopleCodeItem
{
    public string RecordName { get; init; } = string.Empty;

    public string FieldName { get; init; } = string.Empty;

    public string EventName { get; init; } = string.Empty;

    public string LastUpdatedBy { get; init; } = string.Empty;

    public DateTime? LastUpdatedDateTime { get; init; }

    public string DisplayName
    {
        get
        {
            List<string> parts = [];

            if (!string.IsNullOrWhiteSpace(FieldName))
            {
                parts.Add(FieldName);
            }

            if (!string.IsNullOrWhiteSpace(EventName))
            {
                parts.Add(EventName);
            }

            return parts.Count == 0 ? RecordName : string.Join(".", parts);
        }
    }

    public string SearchSummary
    {
        get
        {
            List<string> parts =
            [
                RecordName,
                FieldName,
                EventName
            ];

            return string.Join(" | ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
        }
    }

    public string LevelLabel => string.IsNullOrWhiteSpace(FieldName) ? "Record Event" : "Field Event";
}
