using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PeopleCodeIDECompanion.Models;

namespace PeopleCodeIDECompanion.Services;

public sealed class AllObjectsPeopleCodeBrowserService
{
    public const string AllObjectsMode = "All Objects";
    public const string AppPackageMode = "App Package";
    public const string AppEngineMode = "App Engine";
    public const string RecordMode = "Record";
    public const string PageMode = "Page";
    public const string ComponentMode = "Component";

    private readonly AppPackageBrowserService _appPackageBrowserService = new();
    private readonly AppEngineBrowserService _appEngineBrowserService = new();
    private readonly RecordPeopleCodeBrowserService _recordPeopleCodeBrowserService = new();
    private readonly PagePeopleCodeBrowserService _pagePeopleCodeBrowserService = new();
    private readonly ComponentPeopleCodeBrowserService _componentPeopleCodeBrowserService = new();

    public async Task<AllObjectsSearchResult> SearchAsync(
        OracleConnectionOptions options,
        string searchText,
        int maxResultsPerType,
        CancellationToken cancellationToken = default)
    {
        Task<AppPackageSourceSearchResult> appPackageTask =
            _appPackageBrowserService.SearchSourceAsync(options, searchText, maxResultsPerType, cancellationToken);
        Task<AppEngineSourceSearchResult> appEngineTask =
            _appEngineBrowserService.SearchSourceAsync(options, searchText, maxResultsPerType, cancellationToken);
        Task<RecordPeopleCodeSourceSearchResult> recordTask =
            _recordPeopleCodeBrowserService.SearchSourceAsync(options, searchText, maxResultsPerType, cancellationToken);
        Task<PagePeopleCodeSourceSearchResult> pageTask =
            _pagePeopleCodeBrowserService.SearchSourceAsync(options, searchText, maxResultsPerType, cancellationToken);
        Task<ComponentPeopleCodeSourceSearchResult> componentTask =
            _componentPeopleCodeBrowserService.SearchSourceAsync(options, searchText, maxResultsPerType, cancellationToken);

        await Task.WhenAll(appPackageTask, appEngineTask, recordTask, pageTask, componentTask);

        List<AllObjectsSearchItem> items = [];
        List<string> failureMessages = [];
        bool wasLimited = false;

        AppPackageSourceSearchResult appPackageResult = await appPackageTask;
        if (string.IsNullOrWhiteSpace(appPackageResult.ErrorMessage))
        {
            items.AddRange(appPackageResult.Matches.Select(CreateAppPackageItem));
            wasLimited |= appPackageResult.WasLimited;
        }
        else
        {
            failureMessages.Add($"{AppPackageMode}: {appPackageResult.ErrorMessage}");
        }

        AppEngineSourceSearchResult appEngineResult = await appEngineTask;
        if (string.IsNullOrWhiteSpace(appEngineResult.ErrorMessage))
        {
            items.AddRange(appEngineResult.Matches.Select(CreateAppEngineItem));
            wasLimited |= appEngineResult.WasLimited;
        }
        else
        {
            failureMessages.Add($"{AppEngineMode}: {appEngineResult.ErrorMessage}");
        }

        RecordPeopleCodeSourceSearchResult recordResult = await recordTask;
        if (string.IsNullOrWhiteSpace(recordResult.ErrorMessage))
        {
            items.AddRange(recordResult.Matches.Select(CreateRecordItem));
            wasLimited |= recordResult.WasLimited;
        }
        else
        {
            failureMessages.Add($"{RecordMode}: {recordResult.ErrorMessage}");
        }

        PagePeopleCodeSourceSearchResult pageResult = await pageTask;
        if (string.IsNullOrWhiteSpace(pageResult.ErrorMessage))
        {
            items.AddRange(pageResult.Matches.Select(CreatePageItem));
            wasLimited |= pageResult.WasLimited;
        }
        else
        {
            failureMessages.Add($"{PageMode}: {pageResult.ErrorMessage}");
        }

        ComponentPeopleCodeSourceSearchResult componentResult = await componentTask;
        if (string.IsNullOrWhiteSpace(componentResult.ErrorMessage))
        {
            items.AddRange(componentResult.Matches.Select(CreateComponentItem));
            wasLimited |= componentResult.WasLimited;
        }
        else
        {
            failureMessages.Add($"{ComponentMode}: {componentResult.ErrorMessage}");
        }

        List<AllObjectsSearchGroup> groups = items
            .GroupBy(item => item.ObjectType, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => GetObjectTypeOrder(group.Key))
            .Select(group => new AllObjectsSearchGroup
            {
                ObjectType = group.Key,
                MatchCount = group.Count()
            })
            .ToList();

        List<AllObjectsSearchItem> orderedItems = items
            .OrderBy(item => GetObjectTypeOrder(item.ObjectType))
            .ThenBy(item => item.MetadataTitle, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.PrimaryText, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.SecondaryText, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new AllObjectsSearchResult
        {
            Groups = groups,
            Items = orderedItems,
            FailureMessages = failureMessages,
            WasLimited = wasLimited
        };
    }

    public Task<AllObjectsSourceResult> GetSourceAsync(
        OracleConnectionOptions options,
        AllObjectsSearchItem item,
        CancellationToken cancellationToken = default)
    {
        return item.ObjectType switch
        {
            AppPackageMode => GetAppPackageSourceAsync(options, item, cancellationToken),
            AppEngineMode => GetAppEngineSourceAsync(options, item, cancellationToken),
            RecordMode => GetRecordSourceAsync(options, item, cancellationToken),
            PageMode => GetPageSourceAsync(options, item, cancellationToken),
            ComponentMode => GetComponentSourceAsync(options, item, cancellationToken),
            _ => Task.FromResult(new AllObjectsSourceResult
            {
                ErrorMessage = $"Unsupported object type: {item.ObjectType}."
            })
        };
    }

    private async Task<AllObjectsSourceResult> GetAppPackageSourceAsync(
        OracleConnectionOptions options,
        AllObjectsSearchItem item,
        CancellationToken cancellationToken)
    {
        AppPackageSourceResult result = await _appPackageBrowserService.GetSourceAsync(
            options,
            (AppPackageEntry)item.SourceKey,
            cancellationToken);

        return new AllObjectsSourceResult
        {
            SourceText = result.SourceText,
            ErrorMessage = result.ErrorMessage
        };
    }

    private async Task<AllObjectsSourceResult> GetAppEngineSourceAsync(
        OracleConnectionOptions options,
        AllObjectsSearchItem item,
        CancellationToken cancellationToken)
    {
        AppEngineSourceResult result = await _appEngineBrowserService.GetSourceAsync(
            options,
            (AppEngineItem)item.SourceKey,
            cancellationToken);

        return new AllObjectsSourceResult
        {
            SourceText = result.SourceText,
            ErrorMessage = result.ErrorMessage
        };
    }

    private async Task<AllObjectsSourceResult> GetRecordSourceAsync(
        OracleConnectionOptions options,
        AllObjectsSearchItem item,
        CancellationToken cancellationToken)
    {
        RecordPeopleCodeSourceResult result = await _recordPeopleCodeBrowserService.GetSourceAsync(
            options,
            (RecordPeopleCodeItem)item.SourceKey,
            cancellationToken);

        return new AllObjectsSourceResult
        {
            SourceText = result.SourceText,
            ErrorMessage = result.ErrorMessage
        };
    }

    private async Task<AllObjectsSourceResult> GetPageSourceAsync(
        OracleConnectionOptions options,
        AllObjectsSearchItem item,
        CancellationToken cancellationToken)
    {
        PagePeopleCodeSourceResult result = await _pagePeopleCodeBrowserService.GetSourceAsync(
            options,
            (PagePeopleCodeItem)item.SourceKey,
            cancellationToken);

        return new AllObjectsSourceResult
        {
            SourceText = result.SourceText,
            ErrorMessage = result.ErrorMessage
        };
    }

    private async Task<AllObjectsSourceResult> GetComponentSourceAsync(
        OracleConnectionOptions options,
        AllObjectsSearchItem item,
        CancellationToken cancellationToken)
    {
        ComponentPeopleCodeSourceResult result = await _componentPeopleCodeBrowserService.GetSourceAsync(
            options,
            (ComponentPeopleCodeItem)item.SourceKey,
            cancellationToken);

        return new AllObjectsSourceResult
        {
            SourceText = result.SourceText,
            ErrorMessage = result.ErrorMessage
        };
    }

    private static AllObjectsSearchItem CreateAppPackageItem(AppPackageSourceSearchMatch match)
    {
        AppPackageEntry entry = match.Entry;
        return new AllObjectsSearchItem
        {
            ObjectType = AppPackageMode,
            PrimaryText = entry.DisplayName,
            SecondaryText = entry.EntryType,
            MetadataTitle = entry.DisplayName,
            MetadataSubtitle = entry.EntryType,
            MetadataSummary =
                $"PACKAGE PATH={ValueOrPlaceholder(entry.DisplayName)}, ENTRYTYPE={entry.EntryType}, LASTUPDOPRID={ValueOrPlaceholder(entry.LastUpdatedBy)}, LASTUPDDTTM={entry.LastUpdatedDateTime?.ToString("u") ?? "(blank)"}",
            MatchPreview = match.MatchPreview,
            LastUpdatedBy = entry.LastUpdatedBy,
            LastUpdatedDateTime = entry.LastUpdatedDateTime,
            SourceKey = entry
        };
    }

    private static AllObjectsSearchItem CreateAppEngineItem(AppEngineSourceSearchMatch match)
    {
        AppEngineItem item = match.Item;
        return new AllObjectsSearchItem
        {
            ObjectType = AppEngineMode,
            PrimaryText = item.ProgramName,
            SecondaryText = item.DisplayName,
            MetadataTitle = item.DisplayName,
            MetadataSubtitle = item.ProgramName,
            MetadataSummary =
                $"PROGRAM={ValueOrPlaceholder(item.ProgramName)}, SECTION={ValueOrPlaceholder(item.SectionName)}, STEP={ValueOrPlaceholder(item.StepName)}, ACTION={ValueOrPlaceholder(item.ActionName)}, MARKET={ValueOrPlaceholder(item.Market)}, DBTYPE={ValueOrPlaceholder(item.DatabaseType)}, EFFDT={ValueOrPlaceholder(item.EffectiveDateKey)}, LASTUPDOPRID={ValueOrPlaceholder(item.LastUpdatedBy)}, LASTUPDDTTM={item.LastUpdatedDateTime?.ToString("u") ?? "(blank)"}",
            MatchPreview = match.MatchPreview,
            LastUpdatedBy = item.LastUpdatedBy,
            LastUpdatedDateTime = item.LastUpdatedDateTime,
            SourceKey = item
        };
    }

    private static AllObjectsSearchItem CreateRecordItem(RecordPeopleCodeSourceSearchMatch match)
    {
        RecordPeopleCodeItem item = match.Item;
        return new AllObjectsSearchItem
        {
            ObjectType = RecordMode,
            PrimaryText = item.RecordName,
            SecondaryText = $"{item.DisplayName} | {item.LevelLabel}",
            MetadataTitle = item.DisplayName,
            MetadataSubtitle = item.RecordName,
            MetadataSummary =
                $"RECORD={ValueOrPlaceholder(item.RecordName)}, FIELD={ValueOrPlaceholder(item.FieldName)}, EVENT={ValueOrPlaceholder(item.EventName)}, LEVEL={item.LevelLabel}, LASTUPDOPRID={ValueOrPlaceholder(item.LastUpdatedBy)}, LASTUPDDTTM={item.LastUpdatedDateTime?.ToString("u") ?? "(blank)"}",
            MatchPreview = match.MatchPreview,
            LastUpdatedBy = item.LastUpdatedBy,
            LastUpdatedDateTime = item.LastUpdatedDateTime,
            SourceKey = item
        };
    }

    private static AllObjectsSearchItem CreatePageItem(PagePeopleCodeSourceSearchMatch match)
    {
        PagePeopleCodeItem item = match.Item;
        return new AllObjectsSearchItem
        {
            ObjectType = PageMode,
            PrimaryText = item.PageName,
            SecondaryText = $"{item.DisplayName} | {item.StructureLabel}",
            MetadataTitle = item.DisplayName,
            MetadataSubtitle = $"{item.PageName} | {item.StructureLabel}",
            MetadataSummary = item.BuildMetadataSummary(),
            MatchPreview = match.MatchPreview,
            LastUpdatedBy = item.LastUpdatedBy,
            LastUpdatedDateTime = item.LastUpdatedDateTime,
            SourceKey = item
        };
    }

    private static AllObjectsSearchItem CreateComponentItem(ComponentPeopleCodeSourceSearchMatch match)
    {
        ComponentPeopleCodeItem item = match.Item;
        return new AllObjectsSearchItem
        {
            ObjectType = ComponentMode,
            PrimaryText = item.ComponentName,
            SecondaryText = $"{item.DisplayName} | {item.Market} | {item.StructureLabel}",
            MetadataTitle = item.DisplayName,
            MetadataSubtitle = $"{item.ComponentName} | {item.Market} | {item.StructureLabel}",
            MetadataSummary = item.BuildMetadataSummary(),
            MatchPreview = match.MatchPreview,
            LastUpdatedBy = item.LastUpdatedBy,
            LastUpdatedDateTime = item.LastUpdatedDateTime,
            SourceKey = item
        };
    }

    private static int GetObjectTypeOrder(string objectType)
    {
        return objectType switch
        {
            AppPackageMode => 0,
            AppEngineMode => 1,
            RecordMode => 2,
            PageMode => 3,
            ComponentMode => 4,
            _ => int.MaxValue
        };
    }

    private static string ValueOrPlaceholder(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "(blank)" : value;
    }
}
