using System;
using System.Collections.Generic;
using System.Linq;
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

        PeopleCodeComparePaneViewModel leftPane = CreatePane(request.LeftSession, request.SourceDescriptor);
        SourceLoadOutcome rightSourceOutcome = await LoadSourceAsync(
            request.RightSession.Options,
            request.SourceDescriptor.Identity,
            cancellationToken);

        PeopleCodeComparePaneViewModel rightPane = CreatePane(
            request.RightSession,
            request.SourceDescriptor,
            rightSourceOutcome.StatusMessage);

        DiffBuildResult diffResult = BuildDiff(request.LeftSourceText ?? string.Empty, rightSourceOutcome.SourceText ?? string.Empty);
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
            DiffLines = diffResult.Lines
        };
    }

    private static PeopleCodeComparePaneViewModel CreatePane(
        OracleConnectionSession session,
        PeopleCodeSourceDescriptor descriptor,
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

        for (int index = 0; index < operations.Length; index++)
        {
            DiffOperation current = operations[index];
            if (current.Kind == DiffOperationKind.Delete &&
                index + 1 < operations.Length &&
                operations[index + 1].Kind == DiffOperationKind.Insert)
            {
                DiffOperation next = operations[index + 1];
                lines.Add(CreateLine(leftLineNumber++, current.Text, DiffLineSideKind.Changed, rightLineNumber++, next.Text, DiffLineSideKind.Changed));
                changedCount++;
                index++;
                continue;
            }

            switch (current.Kind)
            {
                case DiffOperationKind.Equal:
                    lines.Add(CreateLine(leftLineNumber++, current.Text, DiffLineSideKind.Unchanged, rightLineNumber++, current.Text, DiffLineSideKind.Unchanged));
                    break;
                case DiffOperationKind.Delete:
                    lines.Add(CreateLine(leftLineNumber++, current.Text, DiffLineSideKind.Removed, null, string.Empty, DiffLineSideKind.Empty));
                    removedCount++;
                    break;
                case DiffOperationKind.Insert:
                    lines.Add(CreateLine(null, string.Empty, DiffLineSideKind.Empty, rightLineNumber++, current.Text, DiffLineSideKind.Added));
                    addedCount++;
                    break;
            }
        }

        string summary = $"{changedCount} changed, {addedCount} added, {removedCount} removed";
        string notice = wasTruncated
            ? $"Diff is limited to the first {MaxDiffLinesPerSide} lines per side to keep compare responsive."
            : string.Empty;

        return new DiffBuildResult
        {
            Lines = lines,
            Summary = summary,
            Notice = notice
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

    private static PeopleCodeCompareDiffLineViewModel CreateLine(
        int? leftLineNumber,
        string leftText,
        DiffLineSideKind leftKind,
        int? rightLineNumber,
        string rightText,
        DiffLineSideKind rightKind)
    {
        return new PeopleCodeCompareDiffLineViewModel
        {
            LeftLineNumber = leftLineNumber,
            LeftText = leftText,
            LeftBackground = CreateBackground(leftKind),
            RightLineNumber = rightLineNumber,
            RightText = rightText,
            RightBackground = CreateBackground(rightKind)
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

        public string Summary { get; init; } = string.Empty;

        public string Notice { get; init; } = string.Empty;
    }

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
}
