using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using PeopleCodeIDECompanion.Models;
using PeopleCodeIDECompanion.Services;
using Windows.Foundation;

namespace PeopleCodeIDECompanion.Views;

public sealed partial class PeopleCodeCompareView : UserControl
{
    private static readonly Brush AddedBackgroundBrush = CreateBrush("#330F9D58");
    private static readonly Brush RemovedBackgroundBrush = CreateBrush("#33D93025");
    private static readonly Brush ChangedBackgroundBrush = CreateBrush("#33C27B0A");
    private static readonly Brush ActiveDiffBackgroundBrush = CreateBrush("#66C48A00");
    private static readonly Brush ActiveDiffForegroundBrush = CreateBrush("#FFF9D8");
    private bool _isSyncingScroll;
    private int _activeDiffIndex = -1;

    public PeopleCodeCompareView(PeopleCodeCompareWindowViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        Loaded += PeopleCodeCompareView_Loaded;
    }

    public PeopleCodeCompareWindowViewModel ViewModel { get; }

    private void CopyLeftButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ClipboardService.CopyText(ViewModel.LeftPane.SourceText);
    }

    private void CopyRightButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ClipboardService.CopyText(ViewModel.RightPane.SourceText);
    }

    private void PeopleCodeCompareView_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        Loaded -= PeopleCodeCompareView_Loaded;
        LeftLineNumbersTextBlock.Text = ViewModel.LeftPane.DisplayLineNumbers;
        RightLineNumbersTextBlock.Text = ViewModel.RightPane.DisplayLineNumbers;
        _activeDiffIndex = ViewModel.DiffCount > 0 ? 0 : -1;
        RefreshCompareViewers();
        if (_activeDiffIndex >= 0)
        {
            ScrollActiveDiffIntoView();
        }
    }

    private void PreviousDiffButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        NavigateDiff(-1);
    }

    private void NextDiffButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        NavigateDiff(1);
    }

    private void LeftSourceScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
    {
        SyncScrollOffsets(LeftSourceScrollViewer, RightSourceScrollViewer);
    }

    private void RightSourceScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
    {
        SyncScrollOffsets(RightSourceScrollViewer, LeftSourceScrollViewer);
    }

    private void NavigateDiff(int direction)
    {
        if (ViewModel.DiffCount == 0)
        {
            return;
        }

        _activeDiffIndex = _activeDiffIndex < 0
            ? 0
            : (_activeDiffIndex + direction + ViewModel.DiffCount) % ViewModel.DiffCount;

        RefreshCompareViewers();
        ScrollActiveDiffIntoView();
    }

    private void RefreshCompareViewers()
    {
        PeopleCodeCompareNavigationPoint? activePoint = _activeDiffIndex >= 0 && _activeDiffIndex < ViewModel.DiffNavigationPoints.Count
            ? ViewModel.DiffNavigationPoints[_activeDiffIndex]
            : null;

        ApplyPaneFormatting(
            LeftSourceRichTextBlock,
            ViewModel.LeftPane.DisplaySourceText,
            ViewModel.LeftPane.AddedRanges,
            ViewModel.LeftPane.RemovedRanges,
            ViewModel.LeftPane.ChangedRanges,
            activePoint?.LeftRange);

        ApplyPaneFormatting(
            RightSourceRichTextBlock,
            ViewModel.RightPane.DisplaySourceText,
            ViewModel.RightPane.AddedRanges,
            ViewModel.RightPane.RemovedRanges,
            ViewModel.RightPane.ChangedRanges,
            activePoint?.RightRange);

        PreviousDiffButton.IsEnabled = ViewModel.DiffCount > 0;
        NextDiffButton.IsEnabled = ViewModel.DiffCount > 0;
        DiffNavigationStatusTextBlock.Text = ViewModel.DiffCount == 0
            ? "No differences"
            : $"Diff {_activeDiffIndex + 1} of {ViewModel.DiffCount}";
    }

    private void ApplyPaneFormatting(
        RichTextBlock sourceViewer,
        string displaySourceText,
        IReadOnlyList<CharacterRange> addedRanges,
        IReadOnlyList<CharacterRange> removedRanges,
        IReadOnlyList<CharacterRange> changedRanges,
        CharacterRange? activeRange)
    {
        PeopleCodeSourceFormatter.ApplyFormatting(
            sourceViewer,
            displaySourceText,
            useSyntaxHighlighting: true,
            highlightedSearchText: null);

        AddHighlighter(sourceViewer, addedRanges, AddedBackgroundBrush, null);
        AddHighlighter(sourceViewer, removedRanges, RemovedBackgroundBrush, null);
        AddHighlighter(sourceViewer, changedRanges, ChangedBackgroundBrush, null);

        if (activeRange.HasValue && activeRange.Value.Length > 0)
        {
            AddHighlighter(sourceViewer, [activeRange.Value], ActiveDiffBackgroundBrush, ActiveDiffForegroundBrush);
        }
    }

    private static void AddHighlighter(
        RichTextBlock sourceViewer,
        IReadOnlyList<CharacterRange> ranges,
        Brush background,
        Brush? foreground)
    {
        if (ranges.Count == 0)
        {
            return;
        }

        TextHighlighter highlighter = new()
        {
            Background = background
        };

        if (foreground is not null)
        {
            highlighter.Foreground = foreground;
        }

        foreach (CharacterRange range in ranges.Where(static range => range.Length > 0))
        {
            highlighter.Ranges.Add(new TextRange(range.StartIndex, range.Length));
        }

        if (highlighter.Ranges.Count > 0)
        {
            sourceViewer.TextHighlighters.Add(highlighter);
        }
    }

    private void SyncScrollOffsets(ScrollViewer source, ScrollViewer target)
    {
        if (_isSyncingScroll)
        {
            return;
        }

        _isSyncingScroll = true;
        target.ChangeView(source.HorizontalOffset, source.VerticalOffset, null, true);
        _isSyncingScroll = false;
    }

    private void ScrollActiveDiffIntoView()
    {
        if (_activeDiffIndex < 0 || _activeDiffIndex >= ViewModel.DiffNavigationPoints.Count)
        {
            return;
        }

        PeopleCodeCompareNavigationPoint activePoint = ViewModel.DiffNavigationPoints[_activeDiffIndex];
        double verticalOffset = CalculateVerticalOffset(
            activePoint.LineIndex,
            Math.Max(1, GetDisplayLineCount(ViewModel.LeftPane.DisplaySourceText)),
            LeftSourceScrollViewer);

        double leftHorizontalOffset = CalculateHorizontalOffset(
            ViewModel.LeftPane.DisplaySourceText,
            activePoint.LeftRange,
            LeftSourceScrollViewer);
        double rightHorizontalOffset = CalculateHorizontalOffset(
            ViewModel.RightPane.DisplaySourceText,
            activePoint.RightRange,
            RightSourceScrollViewer);

        _isSyncingScroll = true;
        LeftSourceScrollViewer.ChangeView(leftHorizontalOffset, verticalOffset, null, false);
        RightSourceScrollViewer.ChangeView(rightHorizontalOffset, verticalOffset, null, false);
        _isSyncingScroll = false;
    }

    private static int GetDisplayLineCount(string displaySourceText)
    {
        if (string.IsNullOrEmpty(displaySourceText))
        {
            return 1;
        }

        return displaySourceText.Count(static character => character == '\n') + 1;
    }

    private static double CalculateVerticalOffset(int lineIndex, int totalLineCount, ScrollViewer scrollViewer)
    {
        double scrollableHeight = Math.Max(0d, scrollViewer.ExtentHeight - scrollViewer.ViewportHeight);
        if (scrollableHeight <= 0d || totalLineCount <= 1)
        {
            return 0d;
        }

        double targetLineRatio = Math.Clamp((double)lineIndex / (totalLineCount - 1), 0d, 1d);
        double verticalPadding = Math.Max(24d, scrollViewer.ViewportHeight * 0.2d);
        return Math.Max(0d, (scrollableHeight * targetLineRatio) - verticalPadding);
    }

    private static double CalculateHorizontalOffset(
        string displaySourceText,
        CharacterRange? range,
        ScrollViewer scrollViewer)
    {
        if (!range.HasValue)
        {
            return 0d;
        }

        int startIndex = Math.Clamp(range.Value.StartIndex, 0, displaySourceText.Length);
        int lastLineBreakIndex = displaySourceText.LastIndexOf('\n', Math.Max(0, startIndex - 1));
        int columnIndex = Math.Max(0, startIndex - lastLineBreakIndex - 1);
        string[] lines = displaySourceText.Split('\n');
        int maxLineLength = Math.Max(1, lines.Max(static line => line.Replace("\r", string.Empty).Length));
        double scrollableWidth = Math.Max(0d, scrollViewer.ExtentWidth - scrollViewer.ViewportWidth);
        if (scrollableWidth <= 0d || maxLineLength <= 1)
        {
            return 0d;
        }

        double targetColumnRatio = Math.Clamp((double)columnIndex / maxLineLength, 0d, 1d);
        double horizontalPadding = Math.Max(24d, scrollViewer.ViewportWidth * 0.1d);
        return Math.Max(0d, (scrollableWidth * targetColumnRatio) - horizontalPadding);
    }

    private static Brush CreateBrush(string colorHex)
    {
        return (Brush)Microsoft.UI.Xaml.Markup.XamlReader.Load(
            $"<SolidColorBrush xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" Color=\"{colorHex}\" />");
    }
}
