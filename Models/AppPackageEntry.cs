using System;
using System.Collections.Generic;
using System.Linq;

namespace PeopleCodeIDECompanion.Models;

public sealed class AppPackageEntry
{
    public string PackageRoot { get; init; } = string.Empty;

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
            List<string> segments = new()
            {
                PackageRoot,
                ObjectValue2,
                ObjectValue3,
                ObjectValue4,
                ObjectValue5,
                ObjectValue6,
                ObjectValue7
            };

            return string.Join(":", segments.Where(segment => !string.IsNullOrWhiteSpace(segment)));
        }
    }

    public string EntryType
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(ObjectValue7) ||
                !string.IsNullOrWhiteSpace(ObjectValue6) ||
                !string.IsNullOrWhiteSpace(ObjectValue5))
            {
                return "Program Entry";
            }

            if (!string.IsNullOrWhiteSpace(ObjectValue4) || !string.IsNullOrWhiteSpace(ObjectValue3))
            {
                return "Class Path";
            }

            if (!string.IsNullOrWhiteSpace(ObjectValue2))
            {
                return "Package Path";
            }

            return "Package Root";
        }
    }
}
