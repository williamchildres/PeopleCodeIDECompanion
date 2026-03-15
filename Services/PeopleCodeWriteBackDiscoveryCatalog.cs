using System.Collections.Generic;
using PeopleCodeIDECompanion.Models;

namespace PeopleCodeIDECompanion.Services;

public static class PeopleCodeWriteBackDiscoveryCatalog
{
    public static IReadOnlyList<PeopleCodeWriteBackDiscoveryPoint> ForObjectType(string objectType)
    {
        return objectType switch
        {
            AllObjectsPeopleCodeBrowserService.AppPackageMode => AppPackagePoints,
            AllObjectsPeopleCodeBrowserService.AppEngineMode => AppEnginePoints,
            AllObjectsPeopleCodeBrowserService.RecordMode => RecordPoints,
            AllObjectsPeopleCodeBrowserService.PageMode => PagePoints,
            AllObjectsPeopleCodeBrowserService.ComponentMode => ComponentPoints,
            _ => SharedPoints
        };
    }

    private static readonly IReadOnlyList<PeopleCodeWriteBackDiscoveryPoint> SharedPoints =
    [
        new()
        {
            ObjectType = "PeopleCode",
            Title = "Validate authoritative write path",
            Detail = "Confirm the exact write-back tables, row ordering rules, and transaction boundary with authoritative PeopleTools guidance before any DML is added."
        },
        new()
        {
            ObjectType = "PeopleCode",
            Title = "Preserve source before mutation",
            Detail = "Capture the original source snapshot and persist a local backup before any future database write-back is attempted."
        }
    ];

    private static readonly IReadOnlyList<PeopleCodeWriteBackDiscoveryPoint> AppPackagePoints =
    [
        new()
        {
            ObjectType = AllObjectsPeopleCodeBrowserService.AppPackageMode,
            Title = "Carry authoritative App Package keys",
            Detail = "Future App Package save must carry the full OBJECTID1..7 and OBJECTVALUE1..7 identity from the authoritative PSPCMPROG row, not only the readable PSPCMTXT path values."
        },
        new()
        {
            ObjectType = AllObjectsPeopleCodeBrowserService.AppPackageMode,
            Title = "Block direct database DML",
            Detail = "Discovery validated PSPCMPROG as the authoritative store for App Package PeopleCode and PROGTXT as a BLOB. Direct text DML is intentionally blocked until a PeopleTools-backed save path can generate valid authoritative program data."
        },
        new()
        {
            ObjectType = AllObjectsPeopleCodeBrowserService.AppPackageMode,
            Title = "Prefer PeopleTools-backed authoring",
            Detail = "The next safe strategy for App Package save is PSIDE or PeopleTools-backed authoring that updates the authoritative program row and lets PeopleTools maintain the readable source projection."
        }
    ];

    private static readonly IReadOnlyList<PeopleCodeWriteBackDiscoveryPoint> AppEnginePoints =
    [
        new()
        {
            ObjectType = AllObjectsPeopleCodeBrowserService.AppEngineMode,
            Title = "Validate program-section-step-action identity",
            Detail = "Confirm the authoritative object key for App Engine PeopleCode before any update logic is written."
        },
        new()
        {
            ObjectType = AllObjectsPeopleCodeBrowserService.AppEngineMode,
            Title = "Confirm compile scope",
            Detail = "Validate whether the future compile path should target project, program, or broader database scope for App Engine changes."
        }
    ];

    private static readonly IReadOnlyList<PeopleCodeWriteBackDiscoveryPoint> RecordPoints =
    [
        new()
        {
            ObjectType = AllObjectsPeopleCodeBrowserService.RecordMode,
            Title = "Validate record-field-event keying",
            Detail = "Confirm the authoritative save key for record and field event PeopleCode before enabling any write-back path."
        }
    ];

    private static readonly IReadOnlyList<PeopleCodeWriteBackDiscoveryPoint> PagePoints =
    [
        new()
        {
            ObjectType = AllObjectsPeopleCodeBrowserService.PageMode,
            Title = "Validate page event variants",
            Detail = "Page PeopleCode has multiple read-side key shapes today. Confirm the exact save-back rules for page-level and page record-field events before implementation."
        }
    ];

    private static readonly IReadOnlyList<PeopleCodeWriteBackDiscoveryPoint> ComponentPoints =
    [
        new()
        {
            ObjectType = AllObjectsPeopleCodeBrowserService.ComponentMode,
            Title = "Validate component record-event structure",
            Detail = "Confirm the authoritative mapping for component PeopleCode object IDs and values before adding any write-back logic."
        }
    ];
}
