using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using PeopleCodeIDECompanion.Models;
using Windows.UI;

namespace PeopleCodeIDECompanion.Services;

public sealed class PeopleCodeCompareService
{
    private const int MaxDiffLinesPerSide = 1200;

    private readonly AppPackageBrowserService _appPackageBrowserService = new();
    private readonly AppEngineBrowserService _appEngineBrowserService = new();
    private readonly RecordPeopleCodeBrowserService _recordPeopleCodeBrowserService = new();
    private readonly PagePeopleCodeBrowserService _pagePeopleCodeBrowserService = new();
    private readonly ComponentPeopleCodeBrowserService _componentPeopleCodeBrowserService = new();

    public async Task<PeopleCodeCompareWindowViewModel> BuildViewModelAsync(
        PeopleCodeCompareRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.LeftSession is null || request.RightSession is null)
        {
            throw new ArgumentException("Both compare sessions are required.", nameof(request));
        }

        if (request.SourceDescriptor?.Identity?.SourceKey is null)
        {
            throw new ArgumentException("A source identity is required.", nameof(request));
        }

        PeopleCodeComparePaneViewModel leftPane = CreatePane(
            request.LeftSession,
            request.SourceDescriptor,
            request.LeftSourceText);
        SourceLoadOutcome rightSourceOutcome = await LoadSourceAsync(
            request.RightSession.Options,
            request.SourceDescriptor.Identity,
            cancellationToken);

        PeopleCodeComparePaneViewModel rightPane = CreatePane(
            request.RightSession,
            request.SourceDescriptor,
            rightSourceOutcome.SourceText,
            rightSourceOutcome.StatusMessage);

        DiffBuildResult diffResult = BuildDiff(request.LeftSourceText ?? string.Empty, rightSourceOutcome.SourceText ?? string.Empty);
        leftPane = CreatePaneWithDocument(leftPane, diffResult.LeftPaneDocument);
        rightPane = CreatePaneWithDocument(rightPane, diffResult.RightPaneDocument);
        string title = string.IsNullOrWhiteSpace(request.SourceDescriptor.ObjectTitle)
            ? request.SourceDescriptor.Identity.ObjectType
            : request.SourceDescriptor.ObjectTitle;
        string subtitle = $"{request.LeftSession.DisplayName} vs {request.RightSession.DisplayName}";
        if (!string.IsNullOrWhiteSpace(request.SourceDescriptor.ObjectSubtitle))
        {
            subtitle = $"{subtitle} | {request.SourceDescriptor.ObjectSubtitle}";
        }

        return new PeopleCodeCompareWindowViewModel
        {
            WindowTitle = BuildWindowTitle(request, subtitle),
            Title = title,
            Subtitle = subtitle,
            DiffSummary = diffResult.Summary,
            DiffNotice = diffResult.Notice,
            LeftPane = leftPane,
            RightPane = rightPane,
            DiffNavigationPoints = diffResult.NavigationPoints,
            DiffLines = diffResult.Lines
        };
    }

    private static PeopleCodeComparePaneViewModel CreatePaneWithDocument(
        PeopleCodeComparePaneViewModel pane,
        PaneDocument document)
    {
        return new PeopleCodeComparePaneViewModel
        {
            ProfileDisplayName = pane.ProfileDisplayName,
            ProfileContext = pane.ProfileContext,
            ObjectType = pane.ObjectType,
            ObjectTitle = pane.ObjectTitle,
            ObjectSubtitle = pane.ObjectSubtitle,
            MetadataSummary = pane.MetadataSummary,
            SourceText = pane.SourceText,
            DisplaySourceText = document.DisplaySourceText,
            DisplayLineNumbers = document.DisplayLineNumbers,
            AddedRanges = document.AddedRanges,
            RemovedRanges = document.RemovedRanges,
            ChangedRanges = document.ChangedRanges,
            StatusMessage = pane.StatusMessage
        };
    }

    private static PeopleCodeComparePaneViewModel CreatePane(
        OracleConnectionSession session,
        PeopleCodeSourceDescriptor descriptor,
        string sourceText,
        string? statusMessage = null)
    {
        return new PeopleCodeComparePaneViewModel
        {
            ProfileDisplayName = session.DisplayName,
            ProfileContext = BuildProfileContext(session),
            ObjectType = descriptor.Identity.ObjectType,
            ObjectTitle = descriptor.ObjectTitle,
            ObjectSubtitle = descriptor.ObjectSubtitle,
            MetadataSummary = descriptor.MetadataSummary,
            SourceText = sourceText ?? string.Empty,
            StatusMessage = statusMessage?.Trim() ?? string.Empty
        };
    }

    private async Task<SourceLoadOutcome> LoadSourceAsync(
        OracleConnectionOptions options,
        PeopleCodeSourceIdentity identity,
        CancellationToken cancellationToken)
    {
        switch (identity.ObjectType)
        {
            case AllObjectsPeopleCodeBrowserService.AppPackageMode:
            {
                AppPackageSourceResult result = await _appPackageBrowserService.GetSourceAsync(
                    options,
                    (AppPackageEntry)identity.SourceKey,
                    cancellationToken);
                return CreateOutcome(identity.ObjectType, result.SourceText, result.ErrorMessage);
            }
            case AllObjectsPeopleCodeBrowserService.AppEngineMode:
            {
                AppEngineSourceResult result = await _appEngineBrowserService.GetSourceAsync(
                    options,
                    (AppEngineItem)identity.SourceKey,
                    cancellationToken);
                return CreateOutcome(identity.ObjectType, result.SourceText, result.ErrorMessage);
            }
            case AllObjectsPeopleCodeBrowserService.RecordMode:
            {
                RecordPeopleCodeSourceResult result = await _recordPeopleCodeBrowserService.GetSourceAsync(
                    options,
                    (RecordPeopleCodeItem)identity.SourceKey,
                    cancellationToken);
                return CreateOutcome(identity.ObjectType, result.SourceText, result.ErrorMessage);
            }
            case AllObjectsPeopleCodeBrowserService.PageMode:
            {
                PagePeopleCodeSourceResult result = await _pagePeopleCodeBrowserService.GetSourceAsync(
                    options,
                    (PagePeopleCodeItem)identity.SourceKey,
                    cancellationToken);
                return CreateOutcome(identity.ObjectType, result.SourceText, result.ErrorMessage);
            }
            case AllObjectsPeopleCodeBrowserService.ComponentMode:
            {
                ComponentPeopleCodeSourceResult result = await _componentPeopleCodeBrowserService.GetSourceAsync(
                    options,
                    (ComponentPeopleCodeItem)identity.SourceKey,
                    cancellationToken);
                return CreateOutcome(identity.ObjectType, result.SourceText, result.ErrorMessage);
            }
            default:
                return new SourceLoadOutcome
                {
                    StatusMessage = $"Unsupported compare object type: {identity.ObjectType}."
                };
        }
    }

    private static SourceLoadOutcome CreateOutcome(string objectType, string? sourceText, string? errorMessage)
    {
        if (!string.IsNullOrWhiteSpace(errorMessage))
        {
            return new SourceLoadOutcome
            {
                StatusMessage = $"{objectType} source could not be loaded. {errorMessage.Trim()}",
                SourceText = string.Empty
            };
        }

        if (string.IsNullOrWhiteSpace(sourceText))
        {
            return new SourceLoadOutcome
            {
                StatusMessage = $"No matching {objectType} source rows were returned for this profile.",
                SourceText = string.Empty
            };
        }

        return new SourceLoadOutcome
        {
            SourceText = sourceText
        };
    }

    private static string BuildProfileContext(OracleConnectionSession session)
    {
        return $"{session.DisplayName} | {session.Options.Username} @ {session.Options.Host}:{session.Options.Port}/{session.Options.ServiceName}";
    }

    private static string BuildWindowTitle(PeopleCodeCompareRequest request, string subtitle)
    {
        string leftRight = $"{request.LeftSession.DisplayName} vs {request.RightSession.DisplayName}";
        string title = string.IsNullOrWhiteSpace(request.SourceDescriptor.ObjectTitle)
            ? $"{request.SourceDescriptor.Identity.ObjectType} Compare"
            : $"{request.SourceDescriptor.Identity.ObjectType}: {request.SourceDescriptor.ObjectTitle}";
        return string.IsNullOrWhiteSpace(request.SourceDescriptor.ObjectSubtitle)
            ? $"{title} | {leftRight}"
            : $"{title} | {subtitle}";
    }

    private static DiffBuildResult BuildDiff(string leftSource, string rightSource)
    {
        List<string> leftLines = SplitLines(leftSource);
        List<string> rightLines = SplitLines(rightSource);
        bool wasTruncated = false;

        if (leftLines.Count > MaxDiffLinesPerSide)
        {
            leftLines = leftLines.Take(MaxDiffLinesPerSide).ToList();
            wasTruncated = true;
        }

        if (rightLines.Count > MaxDiffLinesPerSide)
        {
            rightLines = rightLines.Take(MaxDiffLinesPerSide).ToList();
            wasTruncated = true;
        }

        DiffOperation[] operations = ComputeOperations(leftLines, rightLines);
        List<PeopleCodeCompareDiffLineViewModel> lines = [];
        int leftLineNumber = 1;
        int rightLineNumber = 1;
        int changedCount = 0;
        int addedCount = 0;
        int removedCount = 0;
        List<DiffRow> rows = [];

        for (int index = 0; index < operations.Length; index++)
        {
            DiffOperation current = operations[index];
            if (current.Kind == DiffOperationKind.Delete &&
                index + 1 < operations.Length &&
                operations[index + 1].Kind == DiffOperationKind.Insert)
            {
                DiffOperation next = operations[index + 1];
                DiffRow row = new(leftLineNumber++, current.Text, DiffLineSideKind.Changed, rightLineNumber++, next.Text, DiffLineSideKind.Changed);
                rows.Add(row);
                lines.Add(CreateLine(row));
                changedCount++;
                index++;
                continue;
            }

            switch (current.Kind)
            {
                case DiffOperationKind.Equal:
                {
                    DiffRow row = new(leftLineNumber++, current.Text, DiffLineSideKind.Unchanged, rightLineNumber++, current.Text, DiffLineSideKind.Unchanged);
                    rows.Add(row);
                    lines.Add(CreateLine(row));
                    break;
                }
                case DiffOperationKind.Delete:
                {
                    DiffRow row = new(leftLineNumber++, current.Text, DiffLineSideKind.Removed, null, string.Empty, DiffLineSideKind.Empty);
                    rows.Add(row);
                    lines.Add(CreateLine(row));
                    removedCount++;
                    break;
                }
                case DiffOperationKind.Insert:
                {
                    DiffRow row = new(null, string.Empty, DiffLineSideKind.Empty, rightLineNumber++, current.Text, DiffLineSideKind.Added);
                    rows.Add(row);
                    lines.Add(CreateLine(row));
                    addedCount++;
                    break;
                }
            }
        }

        string summary = $"{changedCount} changed, {addedCount} added, {removedCount} removed";
        string notice = wasTruncated
            ? $"Diff is limited to the first {MaxDiffLinesPerSide} lines per side to keep compare responsive."
            : string.Empty;
        PaneDocument leftPaneDocument = BuildPaneDocument(rows, isLeft: true);
        PaneDocument rightPaneDocument = BuildPaneDocument(rows, isLeft: false);
        IReadOnlyList<PeopleCodeCompareNavigationPoint> navigationPoints = BuildNavigationPoints(rows);

        return new DiffBuildResult
        {
            Lines = lines,
            Summary = summary,
            Notice = notice,
            LeftPaneDocument = leftPaneDocument,
            RightPaneDocument = rightPaneDocument,
            NavigationPoints = navigationPoints
        };
    }

    private static List<string> SplitLines(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return [];
        }

        return value.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').ToList();
    }

    private static DiffOperation[] ComputeOperations(IReadOnlyList<string> leftLines, IReadOnlyList<string> rightLines)
    {
        int[,] lcs = new int[leftLines.Count + 1, rightLines.Count + 1];

        for (int leftIndex = leftLines.Count - 1; leftIndex >= 0; leftIndex--)
        {
            for (int rightIndex = rightLines.Count - 1; rightIndex >= 0; rightIndex--)
            {
                lcs[leftIndex, rightIndex] = string.Equals(leftLines[leftIndex], rightLines[rightIndex], StringComparison.Ordinal)
                    ? lcs[leftIndex + 1, rightIndex + 1] + 1
                    : Math.Max(lcs[leftIndex + 1, rightIndex], lcs[leftIndex, rightIndex + 1]);
            }
        }

        List<DiffOperation> operations = [];
        int leftCursor = 0;
        int rightCursor = 0;

        while (leftCursor < leftLines.Count && rightCursor < rightLines.Count)
        {
            if (string.Equals(leftLines[leftCursor], rightLines[rightCursor], StringComparison.Ordinal))
            {
                operations.Add(new DiffOperation(DiffOperationKind.Equal, leftLines[leftCursor]));
                leftCursor++;
                rightCursor++;
                continue;
            }

            if (lcs[leftCursor + 1, rightCursor] >= lcs[leftCursor, rightCursor + 1])
            {
                operations.Add(new DiffOperation(DiffOperationKind.Delete, leftLines[leftCursor]));
                leftCursor++;
            }
            else
            {
                operations.Add(new DiffOperation(DiffOperationKind.Insert, rightLines[rightCursor]));
                rightCursor++;
            }
        }

        while (leftCursor < leftLines.Count)
        {
            operations.Add(new DiffOperation(DiffOperationKind.Delete, leftLines[leftCursor++]));
        }

        while (rightCursor < rightLines.Count)
        {
            operations.Add(new DiffOperation(DiffOperationKind.Insert, rightLines[rightCursor++]));
        }

        return operations.ToArray();
    }

    private static PaneDocument BuildPaneDocument(IReadOnlyList<DiffRow> rows, bool isLeft)
    {
        StringBuilder sourceBuilder = new();
        StringBuilder lineNumberBuilder = new();
        List<CharacterRange> addedRanges = [];
        List<CharacterRange> removedRanges = [];
        List<CharacterRange> changedRanges = [];
        int position = 0;

        for (int index = 0; index < rows.Count; index++)
        {
            DiffRow row = rows[index];
            string text = isLeft ? row.LeftText : row.RightText;
            int? lineNumber = isLeft ? row.LeftLineNumber : row.RightLineNumber;
            DiffLineSideKind kind = isLeft ? row.LeftKind : row.RightKind;
            int start = position;

            sourceBuilder.Append(text);
            lineNumberBuilder.Append(lineNumber?.ToString() ?? string.Empty);
            position += text.Length;

            if (text.Length > 0)
            {
                CharacterRange range = new(start, text.Length);
                switch (kind)
                {
                    case DiffLineSideKind.Added:
                        addedRanges.Add(range);
                        break;
                    case DiffLineSideKind.Removed:
                        removedRanges.Add(range);
                        break;
                    case DiffLineSideKind.Changed:
                        changedRanges.Add(range);
                        break;
                }
            }

            if (index < rows.Count - 1)
            {
                sourceBuilder.Append('\n');
                lineNumberBuilder.Append('\n');
                position++;
            }
        }

        return new PaneDocument
        {
            DisplaySourceText = sourceBuilder.ToString(),
            DisplayLineNumbers = lineNumberBuilder.ToString(),
            AddedRanges = addedRanges,
            RemovedRanges = removedRanges,
            ChangedRanges = changedRanges
        };
    }

    private static IReadOnlyList<PeopleCodeCompareNavigationPoint> BuildNavigationPoints(IReadOnlyList<DiffRow> rows)
    {
        List<PeopleCodeCompareNavigationPoint> navigationPoints = [];
        int leftPosition = 0;
        int rightPosition = 0;
        int blockStartLineIndex = -1;
        int? leftBlockStart = null;
        int? leftBlockEnd = null;
        int? rightBlockStart = null;
        int? rightBlockEnd = null;
        DiffBlockSide activeBlockSide = DiffBlockSide.None;

        for (int index = 0; index < rows.Count; index++)
        {
            DiffRow row = rows[index];
            bool isDifferent = row.LeftKind != DiffLineSideKind.Unchanged || row.RightKind != DiffLineSideKind.Unchanged;
            DiffBlockSide rowSide = GetBlockSide(row);
            if (isDifferent)
            {
                if (blockStartLineIndex >= 0 &&
                    ShouldSplitBlock(activeBlockSide, rowSide))
                {
                    navigationPoints.Add(CreateNavigationPoint(
                        blockStartLineIndex,
                        leftBlockStart,
                        leftBlockEnd,
                        rightBlockStart,
                        rightBlockEnd));
                    blockStartLineIndex = -1;
                    leftBlockStart = null;
                    leftBlockEnd = null;
                    rightBlockStart = null;
                    rightBlockEnd = null;
                }

                if (blockStartLineIndex < 0)
                {
                    blockStartLineIndex = index;
                    activeBlockSide = rowSide;
                }
                else if (rowSide is DiffBlockSide.LeftOnly or DiffBlockSide.RightOnly)
                {
                    activeBlockSide = rowSide;
                }

                if (row.LeftText.Length > 0)
                {
                    leftBlockStart ??= leftPosition;
                    leftBlockEnd = leftPosition + row.LeftText.Length;
                }

                if (row.RightText.Length > 0)
                {
                    rightBlockStart ??= rightPosition;
                    rightBlockEnd = rightPosition + row.RightText.Length;
                }
            }
            else if (blockStartLineIndex >= 0)
            {
                navigationPoints.Add(CreateNavigationPoint(
                    blockStartLineIndex,
                    leftBlockStart,
                    leftBlockEnd,
                    rightBlockStart,
                    rightBlockEnd));
                blockStartLineIndex = -1;
                leftBlockStart = null;
                leftBlockEnd = null;
                rightBlockStart = null;
                rightBlockEnd = null;
                activeBlockSide = DiffBlockSide.None;
            }

            leftPosition += row.LeftText.Length;
            rightPosition += row.RightText.Length;
            if (index < rows.Count - 1)
            {
                leftPosition++;
                rightPosition++;
            }
        }

        if (blockStartLineIndex >= 0)
        {
            navigationPoints.Add(CreateNavigationPoint(
                blockStartLineIndex,
                leftBlockStart,
                leftBlockEnd,
                rightBlockStart,
                rightBlockEnd));
        }

        return navigationPoints;
    }

    private static PeopleCodeCompareNavigationPoint CreateNavigationPoint(
        int lineIndex,
        int? leftBlockStart,
        int? leftBlockEnd,
        int? rightBlockStart,
        int? rightBlockEnd)
    {
        return new PeopleCodeCompareNavigationPoint
        {
            LineIndex = lineIndex,
            LeftRange = leftBlockStart.HasValue && leftBlockEnd.HasValue && leftBlockEnd.Value > leftBlockStart.Value
                ? new CharacterRange(leftBlockStart.Value, leftBlockEnd.Value - leftBlockStart.Value)
                : null,
            RightRange = rightBlockStart.HasValue && rightBlockEnd.HasValue && rightBlockEnd.Value > rightBlockStart.Value
                ? new CharacterRange(rightBlockStart.Value, rightBlockEnd.Value - rightBlockStart.Value)
                : null
        };
    }

    private static DiffBlockSide GetBlockSide(DiffRow row)
    {
        bool hasLeftDiff = HasDiffContent(row.LeftKind);
        bool hasRightDiff = HasDiffContent(row.RightKind);

        return (hasLeftDiff, hasRightDiff) switch
        {
            (true, false) => DiffBlockSide.LeftOnly,
            (false, true) => DiffBlockSide.RightOnly,
            (true, true) => DiffBlockSide.BothSides,
            _ => DiffBlockSide.None
        };
    }

    private static bool HasDiffContent(DiffLineSideKind kind)
    {
        return kind is DiffLineSideKind.Added or DiffLineSideKind.Removed or DiffLineSideKind.Changed;
    }

    private static bool ShouldSplitBlock(DiffBlockSide activeBlockSide, DiffBlockSide rowSide)
    {
        if (activeBlockSide is DiffBlockSide.None or DiffBlockSide.BothSides ||
            rowSide is DiffBlockSide.None or DiffBlockSide.BothSides)
        {
            return false;
        }

        return activeBlockSide != rowSide;
    }

    private static PeopleCodeCompareDiffLineViewModel CreateLine(DiffRow row)
    {
        return new PeopleCodeCompareDiffLineViewModel
        {
            LeftLineNumber = row.LeftLineNumber,
            LeftText = row.LeftText,
            LeftBackground = CreateBackground(row.LeftKind),
            RightLineNumber = row.RightLineNumber,
            RightText = row.RightText,
            RightBackground = CreateBackground(row.RightKind)
        };
    }

    private static Brush CreateBackground(DiffLineSideKind kind)
    {
        Color color = kind switch
        {
            DiffLineSideKind.Added => Color.FromArgb(0x33, 0x0F, 0x9D, 0x58),
            DiffLineSideKind.Removed => Color.FromArgb(0x33, 0xD9, 0x30, 0x25),
            DiffLineSideKind.Changed => Color.FromArgb(0x33, 0xC2, 0x7B, 0x0A),
            _ => Color.FromArgb(0x00, 0x00, 0x00, 0x00)
        };

        return new SolidColorBrush(color);
    }

    private sealed class SourceLoadOutcome
    {
        public string SourceText { get; init; } = string.Empty;

        public string StatusMessage { get; init; } = string.Empty;
    }

    private sealed class DiffBuildResult
    {
        public IReadOnlyList<PeopleCodeCompareDiffLineViewModel> Lines { get; init; } =
            Array.Empty<PeopleCodeCompareDiffLineViewModel>();

        public PaneDocument LeftPaneDocument { get; init; } = new();

        public PaneDocument RightPaneDocument { get; init; } = new();

        public IReadOnlyList<PeopleCodeCompareNavigationPoint> NavigationPoints { get; init; } =
            Array.Empty<PeopleCodeCompareNavigationPoint>();

        public string Summary { get; init; } = string.Empty;

        public string Notice { get; init; } = string.Empty;
    }

    private sealed class PaneDocument
    {
        public string DisplaySourceText { get; init; } = string.Empty;

        public string DisplayLineNumbers { get; init; } = string.Empty;

        public IReadOnlyList<CharacterRange> AddedRanges { get; init; } = Array.Empty<CharacterRange>();

        public IReadOnlyList<CharacterRange> RemovedRanges { get; init; } = Array.Empty<CharacterRange>();

        public IReadOnlyList<CharacterRange> ChangedRanges { get; init; } = Array.Empty<CharacterRange>();
    }

    private sealed record DiffRow(
        int? LeftLineNumber,
        string LeftText,
        DiffLineSideKind LeftKind,
        int? RightLineNumber,
        string RightText,
        DiffLineSideKind RightKind);

    private readonly record struct DiffOperation(DiffOperationKind Kind, string Text);

    private enum DiffOperationKind
    {
        Equal,
        Delete,
        Insert
    }

    private enum DiffLineSideKind
    {
        Unchanged,
        Added,
        Removed,
        Changed,
        Empty
    }

    private enum DiffBlockSide
    {
        None,
        LeftOnly,
        RightOnly,
        BothSides
    }
}
