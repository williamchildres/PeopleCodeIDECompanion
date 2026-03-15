using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using PeopleCodeIDECompanion.Models;
using PeopleCodeIDECompanion.Services;
using Windows.Foundation;
using Windows.System;
using Windows.UI.Core;

namespace PeopleCodeIDECompanion.Views;

public sealed partial class DetachedSourceView : UserControl
{
    private readonly PeopleCodeAuthoringCapabilityService _capabilityService = new();
    private DetachedPeopleCodeSourceContext _context = new();
    private string _activeSearchText = string.Empty;
    private IReadOnlyList<TextRange> _currentSourceMatchRanges = Array.Empty<TextRange>();
    private int _activeSourceMatchIndex = -1;

    public DetachedSourceView()
    {
        InitializeComponent();
        SourceRichTextBlock.Blocks.Add(new Paragraph());
        AddHandler(KeyDownEvent, new KeyEventHandler(DetachedSourceView_KeyDown), true);
    }

    public DetachedSourceView(DetachedPeopleCodeSourceContext context)
        : this()
    {
        LoadContext(context);
    }

    public void LoadContext(DetachedPeopleCodeSourceContext context)
    {
        _context = context ?? new DetachedPeopleCodeSourceContext();
        _activeSearchText = _context.SearchText ?? string.Empty;
        ObjectTypeTextBlock.Text = _context.ObjectType;
        ObjectTitleTextBlock.Text = _context.ObjectTitle;
        ObjectSubtitleTextBlock.Text = _context.ObjectSubtitle;
        ObjectSubtitleTextBlock.Visibility = string.IsNullOrWhiteSpace(_context.ObjectSubtitle)
            ? Visibility.Collapsed
            : Visibility.Visible;
        ProfileContextTextBlock.Text = _context.ProfileContext;
        LastUpdatedTextBlock.Text = _context.LastUpdatedText;
        LastUpdatedTextBlock.Visibility = string.IsNullOrWhiteSpace(_context.LastUpdatedText)
            ? Visibility.Collapsed
            : Visibility.Visible;
        MetadataSummaryTextBlock.Text = string.IsNullOrWhiteSpace(_context.MetadataSummary)
            ? "No metadata summary is available for this source."
            : _context.MetadataSummary;
        ApplyAuthoringState();
        ApplySourceFormatting();
    }

    private void CopySourceButton_Click(object sender, RoutedEventArgs e)
    {
        ClipboardService.CopyText(_context.SourceText);
    }

    private void PreviousSourceMatchButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateCurrentSourceMatch(-1);
    }

    private void NextSourceMatchButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateCurrentSourceMatch(1);
    }

    private async void DetachedSourceView_KeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key != VirtualKey.F || !IsControlPressed())
        {
            return;
        }

        args.Handled = true;
        await ShowFindDialogAsync();
    }

    private void ApplySourceFormatting()
    {
        _currentSourceMatchRanges = !string.IsNullOrWhiteSpace(_activeSearchText)
            ? PeopleCodeSourceFormatter.GetMatchRanges(_context.SourceText, _activeSearchText)
            : Array.Empty<TextRange>();
        _activeSourceMatchIndex = _currentSourceMatchRanges.Count > 0 ? 0 : -1;

        PeopleCodeSourceFormatter.ApplyFormatting(
            SourceRichTextBlock,
            _context.SourceText,
            _context.UseSyntaxHighlighting,
            string.IsNullOrWhiteSpace(_activeSearchText) ? null : _activeSearchText,
            _activeSourceMatchIndex);

        UpdateSourceMatchChrome();

        if (_activeSourceMatchIndex >= 0)
        {
            ScrollActiveMatchIntoView();
        }
        else
        {
            SourceScrollViewer.ChangeView(0, 0, null, true);
        }
    }

    private void ApplyAuthoringState()
    {
        PeopleCodeAuthoringPresentationState presentationState =
            _capabilityService.CreatePresentationState(
                _context.AuthoringCapabilities,
                _context.SourceIdentity,
                hasLoadedSource: true);

        SaveButton.IsEnabled = presentationState.IsSaveEnabled;
        SaveCompileButton.IsEnabled = presentationState.IsSaveCompileEnabled;
        RevertButton.IsEnabled = presentationState.IsRevertEnabled;

        ToolTipService.SetToolTip(SaveButton, presentationState.SaveToolTip);
        ToolTipService.SetToolTip(SaveCompileButton, presentationState.SaveCompileToolTip);
        ToolTipService.SetToolTip(RevertButton, presentationState.RevertToolTip);

        AuthoringSummaryTextBlock.Text = string.IsNullOrWhiteSpace(presentationState.StatusLabel)
            ? presentationState.Summary
            : $"{presentationState.StatusLabel}: {presentationState.Summary}";
        AuthoringDetailTextBlock.Text = presentationState.Detail;
    }

    private void RefreshSourceViewerFormatting()
    {
        _currentSourceMatchRanges = !string.IsNullOrWhiteSpace(_activeSearchText)
            ? PeopleCodeSourceFormatter.GetMatchRanges(_context.SourceText, _activeSearchText)
            : Array.Empty<TextRange>();

        if (_currentSourceMatchRanges.Count == 0)
        {
            _activeSourceMatchIndex = -1;
        }
        else if (_activeSourceMatchIndex < 0 || _activeSourceMatchIndex >= _currentSourceMatchRanges.Count)
        {
            _activeSourceMatchIndex = 0;
        }

        PeopleCodeSourceFormatter.ApplyFormatting(
            SourceRichTextBlock,
            _context.SourceText,
            _context.UseSyntaxHighlighting,
            string.IsNullOrWhiteSpace(_activeSearchText) ? null : _activeSearchText,
            _activeSourceMatchIndex);

        UpdateSourceMatchChrome();
    }

    private void UpdateSourceMatchChrome()
    {
        bool hasActiveSearch = !string.IsNullOrWhiteSpace(_activeSearchText);
        bool hasNavigableMatches = _currentSourceMatchRanges.Count > 0;
        SourceMatchStatusTextBlock.Visibility = hasActiveSearch ? Visibility.Visible : Visibility.Collapsed;
        PreviousSourceMatchButton.Visibility = hasActiveSearch ? Visibility.Visible : Visibility.Collapsed;
        NextSourceMatchButton.Visibility = hasActiveSearch ? Visibility.Visible : Visibility.Collapsed;
        PreviousSourceMatchButton.IsEnabled = hasNavigableMatches;
        NextSourceMatchButton.IsEnabled = hasNavigableMatches;
        SourceMatchStatusTextBlock.Text = hasNavigableMatches
            ? $"Match {_activeSourceMatchIndex + 1} of {_currentSourceMatchRanges.Count}"
            : hasActiveSearch
                ? "No matches in current source"
                : string.Empty;
    }

    private void NavigateCurrentSourceMatch(int direction)
    {
        if (_currentSourceMatchRanges.Count == 0)
        {
            return;
        }

        _activeSourceMatchIndex = _activeSourceMatchIndex < 0
            ? 0
            : (_activeSourceMatchIndex + direction + _currentSourceMatchRanges.Count) % _currentSourceMatchRanges.Count;

        RefreshSourceViewerFormatting();
        ScrollActiveMatchIntoView();
    }

    private void ScrollActiveMatchIntoView()
    {
        if (_activeSourceMatchIndex < 0 || _activeSourceMatchIndex >= _currentSourceMatchRanges.Count)
        {
            return;
        }

        TextRange activeRange = _currentSourceMatchRanges[_activeSourceMatchIndex];
        int precedingLineBreaks = 0;
        int lastLineBreakIndex = -1;
        int upperBound = Math.Min(activeRange.StartIndex, _context.SourceText.Length);
        for (int index = 0; index < upperBound; index++)
        {
            if (_context.SourceText[index] == '\n')
            {
                precedingLineBreaks++;
                lastLineBreakIndex = index;
            }
        }

        int columnIndex = Math.Max(0, upperBound - lastLineBreakIndex - 1);
        string[] sourceLines = _context.SourceText.Split('\n');
        int totalLineCount = Math.Max(1, sourceLines.Length);
        int maxLineLength = Math.Max(1, sourceLines.Max(static line => line.Replace("\r", string.Empty).Length));

        double scrollableHeight = Math.Max(0d, SourceScrollViewer.ExtentHeight - SourceScrollViewer.ViewportHeight);
        double scrollableWidth = Math.Max(0d, SourceScrollViewer.ExtentWidth - SourceScrollViewer.ViewportWidth);

        double targetLineRatio = totalLineCount <= 1
            ? 0d
            : Math.Clamp((double)precedingLineBreaks / (totalLineCount - 1), 0d, 1d);
        double targetColumnRatio = maxLineLength <= 1
            ? 0d
            : Math.Clamp((double)columnIndex / maxLineLength, 0d, 1d);

        double verticalPadding = Math.Max(24d, SourceScrollViewer.ViewportHeight * 0.2d);
        double horizontalPadding = Math.Max(24d, SourceScrollViewer.ViewportWidth * 0.1d);
        double verticalOffset = Math.Max(0d, (scrollableHeight * targetLineRatio) - verticalPadding);
        double horizontalOffset = Math.Max(0d, (scrollableWidth * targetColumnRatio) - horizontalPadding);

        SourceScrollViewer.ChangeView(horizontalOffset, verticalOffset, null, false);
    }

    private static bool IsControlPressed()
    {
        CoreVirtualKeyStates controlState = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);
        return (controlState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
    }

    private async System.Threading.Tasks.Task ShowFindDialogAsync()
    {
        TextBox searchTextBox = new()
        {
            Header = "Find in current source",
            PlaceholderText = "Enter text to highlight in this source view",
            Text = _activeSearchText
        };
        searchTextBox.SelectAll();

        ContentDialog dialog = new()
        {
            Title = "Find in Source",
            PrimaryButtonText = "Find",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            Content = searchTextBox,
            XamlRoot = XamlRoot
        };

        ContentDialogResult result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        _activeSearchText = searchTextBox.Text.Trim();
        ApplySourceFormatting();
    }
}
