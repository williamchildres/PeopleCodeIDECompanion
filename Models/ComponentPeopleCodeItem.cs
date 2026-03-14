using System;
using System.Collections.Generic;
using System.Linq;

namespace PeopleCodeIDECompanion.Models;

public sealed class ComponentPeopleCodeItem
{
    public int? ObjectId2 { get; init; }

    public int? ObjectId3 { get; init; }

    public int? ObjectId4 { get; init; }

    public int? ObjectId5 { get; init; }

    public int? ObjectId6 { get; init; }

    public int? ObjectId7 { get; init; }

    public string ComponentName { get; init; } = string.Empty;

    public string Market { get; init; } = string.Empty;

    public string ItemName { get; init; } = string.Empty;

    public string EventName { get; init; } = string.Empty;

    public string ObjectValue5 { get; init; } = string.Empty;

    public string ObjectValue6 { get; init; } = string.Empty;

    public string ObjectValue7 { get; init; } = string.Empty;

    public string LastUpdatedBy { get; init; } = string.Empty;

    public DateTime? LastUpdatedDateTime { get; init; }

    public string DisplayName
    {
        get
        {
            List<string> parts = [];

            if (!string.IsNullOrWhiteSpace(ItemName))
            {
                parts.Add(ItemName);
            }

            if (!string.IsNullOrWhiteSpace(EventName))
            {
                parts.Add(EventName);
            }

            return parts.Count == 0 ? "(blank component item key)" : string.Join(".", parts);
        }
    }

    public string SecondaryLabel =>
        string.IsNullOrWhiteSpace(ItemName)
            ? StructureLabel
            : $"Record {ItemName}";

    public string StructureLabel =>
        IsSupportedComponentRecordEvent
            ? "Component Record Event"
            : "Undecoded Component Key";

    public bool IsSupportedComponentRecordEvent =>
        ObjectId2 == 39 &&
        ObjectId3 == 1 &&
        ObjectId4 == 12 &&
        NormalizeZero(ObjectId5) == 0 &&
        NormalizeZero(ObjectId6) == 0 &&
        NormalizeZero(ObjectId7) == 0;

    public string SearchSummary
    {
        get
        {
            List<string> parts =
            [
                ComponentName,
                Market,
                ItemName,
                EventName,
                ObjectValue5,
                ObjectValue6,
                ObjectValue7,
                StructureLabel,
                BuildObjectIdLabel()
            ];

            return string.Join(" | ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
        }
    }

    public string BuildMetadataSummary()
    {
        return
            $"COMPONENT={ValueOrPlaceholder(ComponentName)}, MARKET={ValueOrPlaceholder(Market)}, ITEM={ValueOrPlaceholder(ItemName)}, EVENT={ValueOrPlaceholder(EventName)}, STRUCTURE={StructureLabel}, OBJECTIDS={BuildObjectIdLabel()}, OBJECTVALUES={BuildObjectValueLabel()}, LASTUPDOPRID={ValueOrPlaceholder(LastUpdatedBy)}, LASTUPDDTTM={LastUpdatedDateTime?.ToString("u") ?? "(blank)"}";
    }

    private string BuildObjectIdLabel()
    {
        return string.Join(
            "/",
            new[]
            {
                ValueOrPlaceholder(ObjectId2),
                ValueOrPlaceholder(ObjectId3),
                ValueOrPlaceholder(ObjectId4),
                ValueOrPlaceholder(ObjectId5),
                ValueOrPlaceholder(ObjectId6),
                ValueOrPlaceholder(ObjectId7)
            });
    }

    private string BuildObjectValueLabel()
    {
        return string.Join(
            "/",
            new[]
            {
                ValueOrPlaceholder(ComponentName),
                ValueOrPlaceholder(Market),
                ValueOrPlaceholder(ItemName),
                ValueOrPlaceholder(EventName),
                ValueOrPlaceholder(ObjectValue5),
                ValueOrPlaceholder(ObjectValue6),
                ValueOrPlaceholder(ObjectValue7)
            });
    }

    private static int NormalizeZero(int? value)
    {
        return value ?? 0;
    }

    private static string ValueOrPlaceholder(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "(blank)" : value;
    }

    private static string ValueOrPlaceholder(int? value)
    {
        return value?.ToString() ?? "(blank)";
    }
}
