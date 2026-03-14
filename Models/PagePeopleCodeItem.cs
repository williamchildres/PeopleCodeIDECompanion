using System;
using System.Collections.Generic;
using System.Linq;

namespace PeopleCodeIDECompanion.Models;

public sealed class PagePeopleCodeItem
{
    public string PageName { get; init; } = string.Empty;

    public int? ObjectId2 { get; init; }

    public int? ObjectId3 { get; init; }

    public int? ObjectId4 { get; init; }

    public int? ObjectId5 { get; init; }

    public int? ObjectId6 { get; init; }

    public int? ObjectId7 { get; init; }

    public string ObjectValue2 { get; init; } = string.Empty;

    public string ObjectValue3 { get; init; } = string.Empty;

    public string ObjectValue4 { get; init; } = string.Empty;

    public string ObjectValue5 { get; init; } = string.Empty;

    public string ObjectValue6 { get; init; } = string.Empty;

    public string ObjectValue7 { get; init; } = string.Empty;

    public string LastUpdatedBy { get; init; } = string.Empty;

    public DateTime? LastUpdatedDateTime { get; init; }

    public string DisplayName
    {
        get
        {
            if (IsPageEvent)
            {
                return string.IsNullOrWhiteSpace(ObjectValue2) ? "(blank page event)" : ObjectValue2;
            }

            if (IsPageFieldEvent)
            {
                List<string> parts = [];
                if (!string.IsNullOrWhiteSpace(ObjectValue2))
                {
                    parts.Add(ObjectValue2);
                }

                if (!string.IsNullOrWhiteSpace(ObjectValue3))
                {
                    parts.Add(ObjectValue3);
                }

                if (!string.IsNullOrWhiteSpace(ObjectValue4))
                {
                    parts.Add(ObjectValue4);
                }

                return parts.Count == 0 ? "(blank field event key)" : string.Join(".", parts);
            }

            List<string> genericParts =
            [
                ObjectValue2,
                ObjectValue3,
                ObjectValue4,
                ObjectValue5,
                ObjectValue6,
                ObjectValue7
            ];

            string displayName = string.Join(" / ", genericParts.Where(part => !string.IsNullOrWhiteSpace(part)));
            return string.IsNullOrWhiteSpace(displayName) ? "(undecoded page PeopleCode key)" : displayName;
        }
    }

    public string SecondaryLabel
    {
        get
        {
            if (IsPageEvent)
            {
                return "Page Event";
            }

            if (IsPageFieldEvent)
            {
                return string.IsNullOrWhiteSpace(ObjectValue2)
                    ? "Page Record / Field Event"
                    : $"Record {ObjectValue2}";
            }

            return BuildRawKeyLabel();
        }
    }

    public string SearchSummary
    {
        get
        {
            List<string> parts =
            [
                PageName,
                DisplayName,
                SecondaryLabel,
                ObjectValue2,
                ObjectValue3,
                ObjectValue4,
                ObjectValue5,
                ObjectValue6,
                ObjectValue7,
                BuildObjectIdLabel()
            ];

            return string.Join(" | ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
        }
    }

    public string StructureLabel =>
        IsPageEvent
            ? "Page Event"
            : IsPageFieldEvent
                ? "Page Record Field Event"
                : "Undecoded Page Key";

    public bool IsPageEvent => ObjectId2 == 12 && AllIdsBlank(ObjectId3, ObjectId4, ObjectId5, ObjectId6, ObjectId7);

    public bool IsPageFieldEvent => ObjectId2 == 1 && ObjectId3 == 2 && ObjectId4 == 12;

    public string BuildMetadataSummary()
    {
        return
            $"PAGE={ValueOrPlaceholder(PageName)}, STRUCTURE={StructureLabel}, OBJECTIDS={BuildObjectIdLabel()}, OBJECTVALUES={BuildObjectValueLabel()}, LASTUPDOPRID={ValueOrPlaceholder(LastUpdatedBy)}, LASTUPDDTTM={LastUpdatedDateTime?.ToString("u") ?? "(blank)"}";
    }

    private string BuildRawKeyLabel()
    {
        return $"Object IDs {BuildObjectIdLabel()}";
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
                ValueOrPlaceholder(ObjectValue2),
                ValueOrPlaceholder(ObjectValue3),
                ValueOrPlaceholder(ObjectValue4),
                ValueOrPlaceholder(ObjectValue5),
                ValueOrPlaceholder(ObjectValue6),
                ValueOrPlaceholder(ObjectValue7)
            });
    }

    private static bool AllIdsBlank(params int?[] values)
    {
        return values.All(value => value is null);
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
