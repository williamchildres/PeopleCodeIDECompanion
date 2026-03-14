using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;
using PeopleCodeIDECompanion.Models;

namespace PeopleCodeIDECompanion.Services;

public sealed class PeopleCodeOverviewService
{
    private static readonly TimeSpan UserLookupTimeout = TimeSpan.FromSeconds(10);

    private readonly AppPackageBrowserService _appPackageBrowserService = new();
    private readonly AppEngineBrowserService _appEngineBrowserService = new();
    private readonly RecordPeopleCodeBrowserService _recordPeopleCodeBrowserService = new();
    private readonly PagePeopleCodeBrowserService _pagePeopleCodeBrowserService = new();
    private readonly ComponentPeopleCodeBrowserService _componentPeopleCodeBrowserService = new();

    public async Task<PeopleCodeOverviewDataResult<PeopleCodeOverviewItem>> GetRecentPeopleCodeUpdatesAsync(
        OracleConnectionSession profile,
        TimeSpan lookbackWindow,
        int limit,
        CancellationToken cancellationToken = default)
    {
        OverviewItemLoadResult loadResult = await LoadOverviewItemsAsync(profile, cancellationToken);
        loadResult = await ApplyUserNamesAsync(loadResult, profile.Options, cancellationToken);
        return BuildOverviewResult(
            loadResult,
            ApplyOverviewFilters(loadResult.Items, profile.OverviewSettings)
                .Where(item => IsWithinLookback(item.LastUpdatedDateTime, lookbackWindow))
                .OrderByDescending(item => item.LastUpdatedDateTime ?? DateTime.MinValue)
                .ThenBy(item => item.ObjectType, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.ObjectName, StringComparer.OrdinalIgnoreCase)
                .Take(limit)
                .ToList());
    }

    public async Task<PeopleCodeOverviewDataResult<PeopleCodeAuthorActivityItem>> GetRecentPeopleCodeAuthorsAsync(
        OracleConnectionSession profile,
        TimeSpan lookbackWindow,
        int limit,
        CancellationToken cancellationToken = default)
    {
        OverviewItemLoadResult loadResult = await LoadOverviewItemsAsync(profile, cancellationToken);
        loadResult = await ApplyUserNamesAsync(loadResult, profile.Options, cancellationToken);
        List<PeopleCodeAuthorActivityItem> items = ApplyOverviewFilters(loadResult.Items, profile.OverviewSettings)
            .Where(item => IsWithinLookback(item.LastUpdatedDateTime, lookbackWindow))
            .Where(item => !string.IsNullOrWhiteSpace(item.LastUpdatedBy))
            .GroupBy(item => item.LastUpdatedBy, StringComparer.OrdinalIgnoreCase)
            .Select(group => new PeopleCodeAuthorActivityItem
            {
                Oprid = group.Key,
                DisplayName = group
                    .Select(item => item.LastUpdatedByDisplay)
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
                    ?? group.Key,
                MostRecentUpdateDateTime = group.Max(item => item.LastUpdatedDateTime),
                UpdateCount = group.Count()
            })
            .OrderByDescending(item => item.MostRecentUpdateDateTime ?? DateTime.MinValue)
            .ThenByDescending(item => item.UpdateCount)
            .ThenBy(item => item.Oprid, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToList();

        return new PeopleCodeOverviewDataResult<PeopleCodeAuthorActivityItem>
        {
            Items = items,
            ErrorMessage = loadResult.ErrorMessage,
            WarningMessage = loadResult.WarningMessage
        };
    }

    public async Task<PeopleCodeOverviewDataResult<PeopleCodeOverviewItem>> GetRecentUpdatesByOpridAsync(
        OracleConnectionSession profile,
        string oprid,
        TimeSpan lookbackWindow,
        int limit,
        CancellationToken cancellationToken = default)
    {
        OverviewItemLoadResult loadResult = await LoadOverviewItemsAsync(profile, cancellationToken);
        loadResult = await ApplyUserNamesAsync(loadResult, profile.Options, cancellationToken);
        return BuildOverviewResult(
            loadResult,
            ApplyOverviewFilters(loadResult.Items, profile.OverviewSettings)
                .Where(item => IsWithinLookback(item.LastUpdatedDateTime, lookbackWindow))
                .Where(item => item.LastUpdatedBy.Equals(oprid, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(item => item.LastUpdatedDateTime ?? DateTime.MinValue)
                .ThenBy(item => item.ObjectType, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.ObjectName, StringComparer.OrdinalIgnoreCase)
                .Take(limit)
                .ToList());
    }

    public async Task<PeopleCodeOverviewSnapshotResult> GetOverviewSnapshotAsync(
        OracleConnectionSession profile,
        TimeSpan lookbackWindow,
        int recentItemLimit,
        int recentAuthorLimit,
        CancellationToken cancellationToken = default)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        OverviewItemLoadResult loadResult = await LoadOverviewItemsAsync(profile, cancellationToken);
        loadResult = await ApplyUserNamesAsync(loadResult, profile.Options, cancellationToken);

        List<PeopleCodeOverviewItem> filteredItems = ApplyOverviewFilters(loadResult.Items, profile.OverviewSettings)
            .Where(item => IsWithinLookback(item.LastUpdatedDateTime, lookbackWindow))
            .ToList();

        List<PeopleCodeOverviewItem> recentUpdates = filteredItems
            .OrderByDescending(item => item.LastUpdatedDateTime ?? DateTime.MinValue)
            .ThenBy(item => item.ObjectType, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.ObjectName, StringComparer.OrdinalIgnoreCase)
            .Take(recentItemLimit)
            .ToList();

        List<PeopleCodeAuthorActivityItem> recentAuthors = filteredItems
            .Where(item => !string.IsNullOrWhiteSpace(item.LastUpdatedBy))
            .GroupBy(item => item.LastUpdatedBy, StringComparer.OrdinalIgnoreCase)
            .Select(group => new PeopleCodeAuthorActivityItem
            {
                Oprid = group.Key,
                DisplayName = group
                    .Select(item => item.LastUpdatedByDisplay)
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
                    ?? group.Key,
                MostRecentUpdateDateTime = group.Max(item => item.LastUpdatedDateTime),
                UpdateCount = group.Count()
            })
            .OrderByDescending(item => item.MostRecentUpdateDateTime ?? DateTime.MinValue)
            .ThenByDescending(item => item.UpdateCount)
            .ThenBy(item => item.Oprid, StringComparer.OrdinalIgnoreCase)
            .Take(recentAuthorLimit)
            .ToList();

        return new PeopleCodeOverviewSnapshotResult
        {
            RecentUpdates = recentUpdates,
            RecentAuthors = recentAuthors,
            LoadDuration = stopwatch.Elapsed,
            ErrorMessage = loadResult.ErrorMessage,
            WarningMessage = loadResult.WarningMessage
        };
    }

    public async Task<PeopleCodeRepeatedCodeSearchResult> FindRepeatedCodeBlocksAsync(
        OracleConnectionSession profile,
        string scope,
        PeopleCodeRepeatedCodeSearchOptions options,
        CancellationToken cancellationToken = default)
    {
        _ = scope;
        OverviewItemLoadResult loadResult = await LoadOverviewItemsAsync(profile, cancellationToken);
        loadResult = await ApplyUserNamesAsync(loadResult, profile.Options, cancellationToken);
        if (!string.IsNullOrWhiteSpace(loadResult.ErrorMessage))
        {
            return new PeopleCodeRepeatedCodeSearchResult
            {
                ErrorMessage = loadResult.ErrorMessage
            };
        }

        List<PeopleCodeOverviewItem> scanCandidates = ApplyOverviewFilters(loadResult.Items, profile.OverviewSettings)
            .Where(item => item.SourceKey is not null)
            .OrderByDescending(item => item.LastUpdatedDateTime ?? DateTime.MinValue)
            .ThenBy(item => item.ObjectType, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.ObjectName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        bool wasLimited = scanCandidates.Count > options.MaximumObjectsToScan;
        List<PeopleCodeOverviewItem> itemsToScan = scanCandidates
            .Take(options.MaximumObjectsToScan)
            .ToList();

        Dictionary<string, List<PeopleCodeRepeatedCodeOccurrence>> groupedBlocks = new(StringComparer.Ordinal);
        List<string> sourceFailures = [];

        foreach (PeopleCodeOverviewItem item in itemsToScan)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SourceTextLoadResult sourceResult = await LoadSourceTextAsync(profile.Options, item, cancellationToken);
            if (!string.IsNullOrWhiteSpace(sourceResult.ErrorMessage))
            {
                sourceFailures.Add($"{item.ObjectType} {item.ObjectName}: {sourceResult.ErrorMessage}");
                continue;
            }

            foreach (string block in ExtractNormalizedBlocks(sourceResult.SourceText, options))
            {
                if (!groupedBlocks.TryGetValue(block, out List<PeopleCodeRepeatedCodeOccurrence>? occurrences))
                {
                    occurrences = [];
                    groupedBlocks[block] = occurrences;
                }

                occurrences.Add(new PeopleCodeRepeatedCodeOccurrence
                {
                    Location = item
                });
            }
        }

        List<PeopleCodeRepeatedCodeBlock> blocks = groupedBlocks
            .Where(pair => pair.Value.Count >= 2)
            .Select(pair => new PeopleCodeRepeatedCodeBlock
            {
                NormalizedText = pair.Key,
                SnippetPreview = BuildSnippetPreview(pair.Key),
                Occurrences = pair.Value
                    .OrderBy(occurrence => occurrence.Location.ObjectType, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(occurrence => occurrence.Location.ObjectName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(occurrence => occurrence.Location.Descriptor, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            })
            .OrderByDescending(block => block.OccurrenceCount)
            .ThenByDescending(block => block.NormalizedText.Length)
            .Take(options.MaximumResults)
            .ToList();

        string warningMessage = CombineMessages(
            loadResult.WarningMessage,
            sourceFailures.Count == 0
                ? string.Empty
                : $"Repeated code scan skipped {sourceFailures.Count} object(s): {string.Join(" | ", sourceFailures.Take(5))}{(sourceFailures.Count > 5 ? " | ..." : string.Empty)}");

        return new PeopleCodeRepeatedCodeSearchResult
        {
            Blocks = blocks,
            WarningMessage = warningMessage,
            ScannedObjectCount = itemsToScan.Count,
            WasObjectScanLimited = wasLimited
        };
    }

    private async Task<OverviewItemLoadResult> LoadOverviewItemsAsync(
        OracleConnectionSession profile,
        CancellationToken cancellationToken)
    {
        OracleConnectionOptions options = profile.Options;
        PeopleCodeOverviewProfileSettings settings = PeopleCodeOverviewProfileSettings.Normalize(profile.OverviewSettings);

        Task<AppPackageBrowseResult> appPackageTask = ExecuteWithTimeoutAsync(
            token => _appPackageBrowserService.GetEntriesAsync(options, token),
            errorMessage => new AppPackageBrowseResult { ErrorMessage = errorMessage },
            AllObjectsPeopleCodeBrowserService.AppPackageMode,
            settings.GetTimeout(AllObjectsPeopleCodeBrowserService.AppPackageMode),
            cancellationToken);

        Task<AppEngineBrowseResult> appEngineTask = ExecuteWithTimeoutAsync(
            token => _appEngineBrowserService.GetItemsAsync(options, token),
            errorMessage => new AppEngineBrowseResult { ErrorMessage = errorMessage },
            AllObjectsPeopleCodeBrowserService.AppEngineMode,
            settings.GetTimeout(AllObjectsPeopleCodeBrowserService.AppEngineMode),
            cancellationToken);

        Task<RecordPeopleCodeBrowseResult> recordTask = ExecuteWithTimeoutAsync(
            token => _recordPeopleCodeBrowserService.GetItemsAsync(options, token),
            errorMessage => new RecordPeopleCodeBrowseResult { ErrorMessage = errorMessage },
            AllObjectsPeopleCodeBrowserService.RecordMode,
            settings.GetTimeout(AllObjectsPeopleCodeBrowserService.RecordMode),
            cancellationToken);

        Task<PagePeopleCodeBrowseResult> pageTask = ExecuteWithTimeoutAsync(
            token => _pagePeopleCodeBrowserService.GetItemsAsync(options, token),
            errorMessage => new PagePeopleCodeBrowseResult { ErrorMessage = errorMessage },
            AllObjectsPeopleCodeBrowserService.PageMode,
            settings.GetTimeout(AllObjectsPeopleCodeBrowserService.PageMode),
            cancellationToken);

        Task<ComponentPeopleCodeBrowseResult> componentTask = ExecuteWithTimeoutAsync(
            token => _componentPeopleCodeBrowserService.GetItemsAsync(options, token),
            errorMessage => new ComponentPeopleCodeBrowseResult { ErrorMessage = errorMessage },
            AllObjectsPeopleCodeBrowserService.ComponentMode,
            settings.GetTimeout(AllObjectsPeopleCodeBrowserService.ComponentMode),
            cancellationToken);

        await Task.WhenAll(appPackageTask, appEngineTask, recordTask, pageTask, componentTask);

        List<PeopleCodeOverviewItem> items = [];
        List<string> failures = [];

        AppPackageBrowseResult appPackageResult = await appPackageTask;
        if (string.IsNullOrWhiteSpace(appPackageResult.ErrorMessage))
        {
            items.AddRange(appPackageResult.Entries.Select(CreateAppPackageItem));
        }
        else
        {
            failures.Add($"{AllObjectsPeopleCodeBrowserService.AppPackageMode}: {appPackageResult.ErrorMessage}");
        }

        AppEngineBrowseResult appEngineResult = await appEngineTask;
        if (string.IsNullOrWhiteSpace(appEngineResult.ErrorMessage))
        {
            items.AddRange(appEngineResult.Items.Select(CreateAppEngineItem));
        }
        else
        {
            failures.Add($"{AllObjectsPeopleCodeBrowserService.AppEngineMode}: {appEngineResult.ErrorMessage}");
        }

        RecordPeopleCodeBrowseResult recordResult = await recordTask;
        if (string.IsNullOrWhiteSpace(recordResult.ErrorMessage))
        {
            items.AddRange(recordResult.Items.Select(CreateRecordItem));
        }
        else
        {
            failures.Add($"{AllObjectsPeopleCodeBrowserService.RecordMode}: {recordResult.ErrorMessage}");
        }

        PagePeopleCodeBrowseResult pageResult = await pageTask;
        if (string.IsNullOrWhiteSpace(pageResult.ErrorMessage))
        {
            items.AddRange(pageResult.Items.Select(CreatePageItem));
        }
        else
        {
            failures.Add($"{AllObjectsPeopleCodeBrowserService.PageMode}: {pageResult.ErrorMessage}");
        }

        ComponentPeopleCodeBrowseResult componentResult = await componentTask;
        if (string.IsNullOrWhiteSpace(componentResult.ErrorMessage))
        {
            items.AddRange(componentResult.Items.Select(CreateComponentItem));
        }
        else
        {
            failures.Add($"{AllObjectsPeopleCodeBrowserService.ComponentMode}: {componentResult.ErrorMessage}");
        }

        if (items.Count == 0 && failures.Count > 0)
        {
            return new OverviewItemLoadResult
            {
                ErrorMessage = string.Join(" | ", failures)
            };
        }

        return new OverviewItemLoadResult
        {
            Items = items,
            WarningMessage = failures.Count == 0 ? string.Empty : string.Join(" | ", failures)
        };
    }

    private async Task<OverviewItemLoadResult> ApplyUserNamesAsync(
        OverviewItemLoadResult loadResult,
        OracleConnectionOptions options,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(loadResult.ErrorMessage) || loadResult.Items.Count == 0)
        {
            return loadResult;
        }

        List<string> numericIds = loadResult.Items
            .Select(item => item.LastUpdatedBy)
            .Where(IsNumericOprid)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (numericIds.Count == 0)
        {
            return loadResult;
        }

        UserNameLookupResult userLookupResult = await ResolveUserNamesAsync(options, numericIds, cancellationToken);
        if (!string.IsNullOrWhiteSpace(userLookupResult.ErrorMessage))
        {
            return new OverviewItemLoadResult
            {
                Items = loadResult.Items,
                ErrorMessage = loadResult.ErrorMessage,
                WarningMessage = CombineMessages(loadResult.WarningMessage, userLookupResult.ErrorMessage)
            };
        }

        return new OverviewItemLoadResult
        {
            Items = loadResult.Items
                .Select(item =>
                {
                    if (!IsNumericOprid(item.LastUpdatedBy) ||
                        !userLookupResult.DisplayNamesByOprid.TryGetValue(item.LastUpdatedBy, out string? displayName) ||
                        string.IsNullOrWhiteSpace(displayName))
                    {
                        return item;
                    }

                    return new PeopleCodeOverviewItem
                    {
                        ObjectType = item.ObjectType,
                        ObjectName = item.ObjectName,
                        Descriptor = item.Descriptor,
                        LastUpdatedBy = item.LastUpdatedBy,
                        LastUpdatedByDisplay = $"{displayName} ({item.LastUpdatedBy})",
                        LastUpdatedDateTime = item.LastUpdatedDateTime,
                        SourceKey = item.SourceKey
                    };
                })
                .ToList(),
            ErrorMessage = loadResult.ErrorMessage,
            WarningMessage = CombineMessages(loadResult.WarningMessage, userLookupResult.WarningMessage)
        };
    }

    private async Task<UserNameLookupResult> ResolveUserNamesAsync(
        OracleConnectionOptions options,
        IReadOnlyList<string> oprids,
        CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(UserLookupTimeout);

        try
        {
            await using OracleConnection connection = new(OracleConnectionStringFactory.Create(options));
            await connection.OpenAsync(timeoutCts.Token);

            await using OracleCommand command = connection.CreateCommand();
            command.BindByName = true;

            List<string> parameterNames = [];
            for (int index = 0; index < oprids.Count; index++)
            {
                string parameterName = $"emplid{index}";
                parameterNames.Add($":{parameterName}");
                command.Parameters.Add(parameterName, OracleDbType.Varchar2, oprids[index], System.Data.ParameterDirection.Input);
            }

            command.CommandText = $"""
SELECT EMPLID, NAME_TYPE, NAME
FROM PS_NAMES
WHERE EMPLID IN ({string.Join(", ", parameterNames)})
""";

            Dictionary<string, List<(string NameType, string Name)>> namesByOprid = new(StringComparer.OrdinalIgnoreCase);
            await using OracleDataReader reader = await command.ExecuteReaderAsync(timeoutCts.Token);
            while (await reader.ReadAsync(timeoutCts.Token))
            {
                string emplid = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                string nameType = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                string name = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);

                if (string.IsNullOrWhiteSpace(emplid) || string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                if (!namesByOprid.TryGetValue(emplid, out List<(string NameType, string Name)>? names))
                {
                    names = [];
                    namesByOprid[emplid] = names;
                }

                names.Add((nameType, name));
            }

            Dictionary<string, string> displayNames = namesByOprid.ToDictionary(
                pair => pair.Key,
                pair => SelectPreferredName(pair.Value),
                StringComparer.OrdinalIgnoreCase);

            return new UserNameLookupResult
            {
                DisplayNamesByOprid = displayNames
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new UserNameLookupResult
            {
                ErrorMessage = $"User-name lookup timed out after {UserLookupTimeout.TotalSeconds:0} seconds."
            };
        }
        catch (Exception exception)
        {
            return new UserNameLookupResult
            {
                ErrorMessage = $"User-name lookup failed: {exception.Message}"
            };
        }
    }

    private async Task<SourceTextLoadResult> LoadSourceTextAsync(
        OracleConnectionOptions options,
        PeopleCodeOverviewItem item,
        CancellationToken cancellationToken)
    {
        try
        {
            return item.ObjectType switch
            {
                AllObjectsPeopleCodeBrowserService.AppPackageMode => await LoadAppPackageSourceAsync(options, item, cancellationToken),
                AllObjectsPeopleCodeBrowserService.AppEngineMode => await LoadAppEngineSourceAsync(options, item, cancellationToken),
                AllObjectsPeopleCodeBrowserService.RecordMode => await LoadRecordSourceAsync(options, item, cancellationToken),
                AllObjectsPeopleCodeBrowserService.PageMode => await LoadPageSourceAsync(options, item, cancellationToken),
                AllObjectsPeopleCodeBrowserService.ComponentMode => await LoadComponentSourceAsync(options, item, cancellationToken),
                _ => new SourceTextLoadResult
                {
                    ErrorMessage = $"Unsupported object type: {item.ObjectType}."
                }
            };
        }
        catch (Exception exception)
        {
            return new SourceTextLoadResult
            {
                ErrorMessage = exception.Message
            };
        }
    }

    private async Task<SourceTextLoadResult> LoadAppPackageSourceAsync(
        OracleConnectionOptions options,
        PeopleCodeOverviewItem item,
        CancellationToken cancellationToken)
    {
        AppPackageSourceResult result = await _appPackageBrowserService.GetSourceAsync(
            options,
            (AppPackageEntry)item.SourceKey!,
            cancellationToken);

        return new SourceTextLoadResult
        {
            SourceText = result.SourceText,
            ErrorMessage = result.ErrorMessage
        };
    }

    private async Task<SourceTextLoadResult> LoadAppEngineSourceAsync(
        OracleConnectionOptions options,
        PeopleCodeOverviewItem item,
        CancellationToken cancellationToken)
    {
        AppEngineSourceResult result = await _appEngineBrowserService.GetSourceAsync(
            options,
            (AppEngineItem)item.SourceKey!,
            cancellationToken);

        return new SourceTextLoadResult
        {
            SourceText = result.SourceText,
            ErrorMessage = result.ErrorMessage
        };
    }

    private async Task<SourceTextLoadResult> LoadRecordSourceAsync(
        OracleConnectionOptions options,
        PeopleCodeOverviewItem item,
        CancellationToken cancellationToken)
    {
        RecordPeopleCodeSourceResult result = await _recordPeopleCodeBrowserService.GetSourceAsync(
            options,
            (RecordPeopleCodeItem)item.SourceKey!,
            cancellationToken);

        return new SourceTextLoadResult
        {
            SourceText = result.SourceText,
            ErrorMessage = result.ErrorMessage
        };
    }

    private async Task<SourceTextLoadResult> LoadPageSourceAsync(
        OracleConnectionOptions options,
        PeopleCodeOverviewItem item,
        CancellationToken cancellationToken)
    {
        PagePeopleCodeSourceResult result = await _pagePeopleCodeBrowserService.GetSourceAsync(
            options,
            (PagePeopleCodeItem)item.SourceKey!,
            cancellationToken);

        return new SourceTextLoadResult
        {
            SourceText = result.SourceText,
            ErrorMessage = result.ErrorMessage
        };
    }

    private async Task<SourceTextLoadResult> LoadComponentSourceAsync(
        OracleConnectionOptions options,
        PeopleCodeOverviewItem item,
        CancellationToken cancellationToken)
    {
        ComponentPeopleCodeSourceResult result = await _componentPeopleCodeBrowserService.GetSourceAsync(
            options,
            (ComponentPeopleCodeItem)item.SourceKey!,
            cancellationToken);

        return new SourceTextLoadResult
        {
            SourceText = result.SourceText,
            ErrorMessage = result.ErrorMessage
        };
    }

    private static IEnumerable<string> ExtractNormalizedBlocks(
        string sourceText,
        PeopleCodeRepeatedCodeSearchOptions options)
    {
        List<string> blocks = [];
        List<string> currentBlock = [];

        foreach (string rawLine in sourceText.Replace("\r\n", "\n").Split('\n'))
        {
            string normalizedLine = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(normalizedLine))
            {
                TryAddBlock(blocks, currentBlock, options);
                currentBlock.Clear();
                continue;
            }

            currentBlock.Add(normalizedLine);
        }

        TryAddBlock(blocks, currentBlock, options);
        return blocks;
    }

    private static void TryAddBlock(
        List<string> blocks,
        List<string> lines,
        PeopleCodeRepeatedCodeSearchOptions options)
    {
        if (lines.Count < options.MinimumLinesPerBlock)
        {
            return;
        }

        string block = string.Join('\n', lines);
        if (block.Length < options.MinimumCharactersPerBlock)
        {
            return;
        }

        blocks.Add(block);
    }

    private static string BuildSnippetPreview(string normalizedText)
    {
        const int maxLength = 280;
        string preview = normalizedText.Replace('\n', ' ').Trim();
        return preview.Length <= maxLength ? preview : preview[..maxLength] + "...";
    }

    private static bool IsWithinLookback(DateTime? timestamp, TimeSpan lookbackWindow)
    {
        return timestamp is not null && timestamp.Value >= DateTime.Now.Subtract(lookbackWindow);
    }

    private static IEnumerable<PeopleCodeOverviewItem> ApplyOverviewFilters(
        IEnumerable<PeopleCodeOverviewItem> items,
        PeopleCodeOverviewProfileSettings? settings)
    {
        PeopleCodeOverviewProfileSettings normalized = PeopleCodeOverviewProfileSettings.Normalize(settings);
        return normalized.IgnorePplsoftModifiedObjects
            ? items.Where(item => !item.LastUpdatedBy.Equals("PPLSOFT", StringComparison.OrdinalIgnoreCase))
            : items;
    }

    private static PeopleCodeOverviewDataResult<PeopleCodeOverviewItem> BuildOverviewResult(
        OverviewItemLoadResult loadResult,
        List<PeopleCodeOverviewItem> items)
    {
        return new PeopleCodeOverviewDataResult<PeopleCodeOverviewItem>
        {
            Items = items,
            ErrorMessage = loadResult.ErrorMessage,
            WarningMessage = loadResult.WarningMessage
        };
    }

    private static string CombineMessages(string first, string second)
    {
        if (string.IsNullOrWhiteSpace(first))
        {
            return second;
        }

        if (string.IsNullOrWhiteSpace(second))
        {
            return first;
        }

        return $"{first} | {second}";
    }

    private static PeopleCodeOverviewItem CreateAppPackageItem(AppPackageEntry entry)
    {
        return new PeopleCodeOverviewItem
        {
            ObjectType = AllObjectsPeopleCodeBrowserService.AppPackageMode,
            ObjectName = entry.DisplayName,
            Descriptor = entry.EntryType,
            LastUpdatedBy = entry.LastUpdatedBy,
            LastUpdatedByDisplay = entry.LastUpdatedBy,
            LastUpdatedDateTime = entry.LastUpdatedDateTime,
            SourceKey = entry
        };
    }

    private static PeopleCodeOverviewItem CreateAppEngineItem(AppEngineItem item)
    {
        return new PeopleCodeOverviewItem
        {
            ObjectType = AllObjectsPeopleCodeBrowserService.AppEngineMode,
            ObjectName = item.ProgramName,
            Descriptor = item.DisplayName,
            LastUpdatedBy = item.LastUpdatedBy,
            LastUpdatedByDisplay = item.LastUpdatedBy,
            LastUpdatedDateTime = item.LastUpdatedDateTime,
            SourceKey = item
        };
    }

    private static PeopleCodeOverviewItem CreateRecordItem(RecordPeopleCodeItem item)
    {
        return new PeopleCodeOverviewItem
        {
            ObjectType = AllObjectsPeopleCodeBrowserService.RecordMode,
            ObjectName = item.RecordName,
            Descriptor = item.DisplayName,
            LastUpdatedBy = item.LastUpdatedBy,
            LastUpdatedByDisplay = item.LastUpdatedBy,
            LastUpdatedDateTime = item.LastUpdatedDateTime,
            SourceKey = item
        };
    }

    private static PeopleCodeOverviewItem CreatePageItem(PagePeopleCodeItem item)
    {
        return new PeopleCodeOverviewItem
        {
            ObjectType = AllObjectsPeopleCodeBrowserService.PageMode,
            ObjectName = item.PageName,
            Descriptor = item.DisplayName,
            LastUpdatedBy = item.LastUpdatedBy,
            LastUpdatedByDisplay = item.LastUpdatedBy,
            LastUpdatedDateTime = item.LastUpdatedDateTime,
            SourceKey = item
        };
    }

    private static PeopleCodeOverviewItem CreateComponentItem(ComponentPeopleCodeItem item)
    {
        return new PeopleCodeOverviewItem
        {
            ObjectType = AllObjectsPeopleCodeBrowserService.ComponentMode,
            ObjectName = item.ComponentName,
            Descriptor = item.DisplayName,
            LastUpdatedBy = item.LastUpdatedBy,
            LastUpdatedByDisplay = item.LastUpdatedBy,
            LastUpdatedDateTime = item.LastUpdatedDateTime,
            SourceKey = item
        };
    }

    private static bool IsNumericOprid(string value)
    {
        return !string.IsNullOrWhiteSpace(value) && value.All(char.IsDigit);
    }

    private static string SelectPreferredName(IReadOnlyList<(string NameType, string Name)> names)
    {
        (string NameType, string Name)? preferred = names.FirstOrDefault(name =>
            name.NameType.Equals("PRF", StringComparison.OrdinalIgnoreCase));
        if (preferred is not null && !string.IsNullOrWhiteSpace(preferred.Value.Name))
        {
            return preferred.Value.Name;
        }

        preferred = names.FirstOrDefault(name =>
            name.NameType.Equals("PRI", StringComparison.OrdinalIgnoreCase));
        if (preferred is not null && !string.IsNullOrWhiteSpace(preferred.Value.Name))
        {
            return preferred.Value.Name;
        }

        return names
            .Select(name => name.Name)
            .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name))
            ?? string.Empty;
    }

    private static async Task<T> ExecuteWithTimeoutAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        Func<string, T> errorFactory,
        string operationName,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            return await operation(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return errorFactory($"{operationName} timed out after {timeout.TotalSeconds:0} seconds.");
        }
        catch (Exception exception)
        {
            return errorFactory(exception.Message);
        }
    }

    private sealed class OverviewItemLoadResult
    {
        public IReadOnlyList<PeopleCodeOverviewItem> Items { get; init; } = [];

        public string ErrorMessage { get; init; } = string.Empty;

        public string WarningMessage { get; init; } = string.Empty;
    }

    private sealed class SourceTextLoadResult
    {
        public string SourceText { get; init; } = string.Empty;

        public string ErrorMessage { get; init; } = string.Empty;
    }

    private sealed class UserNameLookupResult
    {
        public IReadOnlyDictionary<string, string> DisplayNamesByOprid { get; init; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public string ErrorMessage { get; init; } = string.Empty;

        public string WarningMessage { get; init; } = string.Empty;
    }
}
