using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using PeopleCodeIDECompanion.Models;
using PeopleCodeIDECompanion.Services;
using Windows.Foundation;
using Windows.System;

namespace PeopleCodeIDECompanion.Views;

public sealed partial class AppEnginePlaceholderView : UserControl
{
    private const int GlobalSearchResultLimit = 200;

    private readonly AppEngineBrowserService _browserService = new();
    private readonly List<AppEngineItem> _allItems = [];
    private readonly ObservableCollection<string> _filteredPrograms = [];
    private readonly ObservableCollection<AppEngineItem> _filteredItems = [];
    private readonly ObservableCollection<AppEngineSourceSearchMatch> _globalSearchResults = [];

    private OracleConnectionSession? _session;
    private AppEngineItem? _selectedItem;
    private int _globalSearchVersion;
    private int _sourceLoadVersion;
    private bool _isGlobalSearchMode;
    private string _activeGlobalSearchText = string.Empty;
    private string _currentSourceText = string.Empty;
    private IReadOnlyList<TextRange> _currentSourceMatchRanges = Array.Empty<TextRange>();
    private int _activeSourceMatchIndex = -1;

    public AppEnginePlaceholderView()
    {
        InitializeComponent();
        ProgramsListView.ItemsSource = _filteredPrograms;
        ItemsListView.ItemsSource = _filteredItems;
        SourceRichTextBlock.Blocks.Add(new Paragraph());
        SetMetadata(null);
        UpdateGlobalSearchChrome();
        UpdateSourceMatchChrome();
    }

    public void SetSession(OracleConnectionSession session)
    {
        _session = session;
        string profileLabel = string.IsNullOrWhiteSpace(session.DisplayName)
            ? $"as {session.Options.Username} to {session.Options.Host}:{session.Options.Port}/{session.Options.ServiceName}"
            : $"with profile {session.DisplayName} as {session.Options.Username} to {session.Options.Host}:{session.Options.Port}/{session.Options.ServiceName}";
        ConnectionStateTextBlock.Text = $"Connected {profileLabel}. App Engine browsing remains read-only.";
        RefreshButton.IsEnabled = true;
        GlobalSourceSearchButton.IsEnabled = true;
        _ = LoadItemsAsync();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadItemsAsync();
    }

    private void ProgramSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyProgramFilter();
    }

    private void ItemSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyItemFilter();
    }

    private void ProgramsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplyItemFilter();
    }

    private async void ItemsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ItemsListView.SelectedItem is not AppEngineItem item)
        {
            _selectedItem = null;
            _sourceLoadVersion++;
            SetMetadata(null);
            SetSourceViewerText(string.Empty);
            return;
        }

        _selectedItem = item;
        SetMetadata(item);

        if (_session is null)
        {
            return;
        }

        int sourceLoadVersion = ++_sourceLoadVersion;
        MetadataSummaryTextBlock.Text = "Loading App Engine source...";

        AppEngineSourceResult result = await _browserService.GetSourceAsync(_session.Options, item);
        if (sourceLoadVersion != _sourceLoadVersion || !ReferenceEquals(_selectedItem, item))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            InlineErrorInfoBar.Message = result.ErrorMessage;
            InlineErrorInfoBar.IsOpen = true;
            MetadataSummaryTextBlock.Text = "App Engine source could not be loaded.";
            SetSourceViewerText(string.Empty);
            return;
        }

        MetadataSummaryTextBlock.Text = string.IsNullOrWhiteSpace(result.SourceText)
            ? BuildMetadataText(item) + " No source rows were returned for the selected key."
            : BuildMetadataText(item);

        SetSourceViewerText(result.SourceText);
    }

    private async void GlobalSourceSearchButton_Click(object sender, RoutedEventArgs e)
    {
        await SearchGlobalSourceAsync();
    }

    private async void GlobalSourceSearchTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter)
        {
            return;
        }

        e.Handled = true;
        await SearchGlobalSourceAsync();
    }

    private void ClearGlobalSearchButton_Click(object sender, RoutedEventArgs e)
    {
        ClearGlobalSearchMode();
    }

    private void PreviousSourceMatchButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateCurrentSourceMatch(-1);
    }

    private void NextSourceMatchButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateCurrentSourceMatch(1);
    }

    private async Task LoadItemsAsync()
    {
        if (_session is null)
        {
            return;
        }

        InlineErrorInfoBar.IsOpen = false;
        GlobalSearchErrorInfoBar.IsOpen = false;
        _allItems.Clear();
        _filteredPrograms.Clear();
        _filteredItems.Clear();
        _globalSearchResults.Clear();
        _selectedItem = null;
        _isGlobalSearchMode = false;
        _activeGlobalSearchText = string.Empty;
        GlobalSourceSearchTextBox.Text = string.Empty;
        SetGlobalSearchStatus(string.Empty, false);
        SetMetadata(null);
        SetSourceViewerText(string.Empty);
        UpdateGlobalSearchChrome();
        MetadataSummaryTextBlock.Text = "Loading App Engine metadata...";

        AppEngineBrowseResult result = await _browserService.GetItemsAsync(_session.Options);
        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            MetadataSummaryTextBlock.Text = "App Engine metadata could not be loaded.";
            InlineErrorInfoBar.Message = result.ErrorMessage;
            InlineErrorInfoBar.IsOpen = true;
            return;
        }

        _allItems.AddRange(result.Items);
        ApplyProgramFilter();
        SelectVisibleProgram(null);
        ApplyItemFilter();
        SelectVisibleItem(null);

        if (_allItems.Count == 0)
        {
            MetadataSummaryTextBlock.Text =
                "No App Engine PeopleCode rows were returned for the current read-only subset. This browser assumes OBJECTVALUE1..7 map to Program / Section / Market / DB Type / EffDt / Step / Action for OBJECTID1 = 66.";
        }
    }

    private void ApplyProgramFilter()
    {
        string? previouslySelectedProgram = ProgramsListView.SelectedItem as string;
        string searchText = ProgramSearchTextBox.Text.Trim();

        _filteredPrograms.Clear();

        IEnumerable<string> programs = GetVisibleItems()
            .Select(item => item.ProgramName)
            .Where(programName => !string.IsNullOrWhiteSpace(programName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(programName => programName, StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            programs = programs.Where(programName =>
                programName.Contains(searchText, StringComparison.OrdinalIgnoreCase));
        }

        foreach (string program in programs)
        {
            _filteredPrograms.Add(program);
        }

        NoProgramsTextBlock.Text = _isGlobalSearchMode
            ? "No programs match the current global PeopleCode search."
            : "No programs found.";
        NoProgramsTextBlock.Visibility = _filteredPrograms.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        SelectVisibleProgram(previouslySelectedProgram);
        ApplyItemFilter();
    }

    private void ApplyItemFilter()
    {
        string? selectedProgram = ProgramsListView.SelectedItem as string;
        AppEngineItem? preferredItem = _selectedItem;
        string searchText = ItemSearchTextBox.Text.Trim();

        _filteredItems.Clear();

        IEnumerable<AppEngineItem> items = GetVisibleItems();
        if (!string.IsNullOrWhiteSpace(selectedProgram))
        {
            items = items.Where(item =>
                item.ProgramName.Equals(selectedProgram, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            items = [];
        }

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            items = items.Where(item => ItemMatchesSearch(item, searchText));
        }

        foreach (AppEngineItem item in items)
        {
            _filteredItems.Add(item);
        }

        NoItemsTextBlock.Text = _isGlobalSearchMode
            ? "No matching items found for the selected program."
            : "No items found.";
        NoItemsTextBlock.Visibility =
            !string.IsNullOrWhiteSpace(selectedProgram) && _filteredItems.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;

        SelectVisibleItem(preferredItem);

        if (_filteredItems.Count == 0)
        {
            _selectedItem = null;
            _sourceLoadVersion++;
            SetMetadata(null);
            SetSourceViewerText(string.Empty);
        }
    }

    private async Task SearchGlobalSourceAsync()
    {
        if (_session is null)
        {
            return;
        }

        string searchText = GlobalSourceSearchTextBox.Text.Trim();
        int searchVersion = ++_globalSearchVersion;

        GlobalSearchErrorInfoBar.IsOpen = false;
        _globalSearchResults.Clear();

        if (string.IsNullOrWhiteSpace(searchText))
        {
            SetGlobalSearchStatus("Enter source text to search.", true);
            return;
        }

        SetGlobalSearchStatus("Searching App Engine PeopleCode...", true);
        GlobalSourceSearchButton.IsEnabled = false;
        UpdateGlobalSearchChrome();

        AppEngineSourceSearchResult result = await _browserService.SearchSourceAsync(
            _session.Options,
            searchText,
            GlobalSearchResultLimit);

        if (searchVersion != _globalSearchVersion)
        {
            return;
        }

        GlobalSourceSearchButton.IsEnabled = true;
        UpdateGlobalSearchChrome();

        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            SetGlobalSearchStatus(string.Empty, false);
            GlobalSearchErrorInfoBar.Message = result.ErrorMessage;
            GlobalSearchErrorInfoBar.IsOpen = true;
            return;
        }

        foreach (AppEngineSourceSearchMatch match in result.Matches)
        {
            _globalSearchResults.Add(match);
        }

        EnterGlobalSearchMode(searchText, result.WasLimited);
    }

    private void EnterGlobalSearchMode(string searchText, bool wasLimited)
    {
        _isGlobalSearchMode = true;
        _activeGlobalSearchText = searchText;
        UpdateGlobalSearchChrome();
        RefreshSourceViewerFormatting();

        string? preferredProgram = _selectedItem?.ProgramName;
        AppEngineItem? preferredItem = _selectedItem;

        ApplyProgramFilter();
        SelectVisibleProgram(preferredProgram);
        ApplyItemFilter();
        SelectVisibleItem(preferredItem);

        int programCount = _globalSearchResults
            .Select(match => match.Item.ProgramName)
            .Where(programName => !string.IsNullOrWhiteSpace(programName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        string countMessage = _globalSearchResults.Count == 0
            ? $"Global search mode is active for \"{searchText}\". No matching items were found."
            : $"Global search mode is active for \"{searchText}\". {_globalSearchResults.Count} matching item(s) across {programCount} program(s).";
        string limitMessage = wasLimited
            ? $" Showing the first {_globalSearchResults.Count} matches."
            : string.Empty;
        SetGlobalSearchStatus(countMessage + limitMessage, true);
    }

    private void ClearGlobalSearchMode()
    {
        _isGlobalSearchMode = false;
        _activeGlobalSearchText = string.Empty;
        _globalSearchResults.Clear();
        GlobalSourceSearchTextBox.Text = string.Empty;
        GlobalSearchErrorInfoBar.IsOpen = false;
        SetGlobalSearchStatus(string.Empty, false);
        UpdateGlobalSearchChrome();
        RefreshSourceViewerFormatting();

        string? preferredProgram = _selectedItem?.ProgramName;
        AppEngineItem? preferredItem = _selectedItem;

        ApplyProgramFilter();
        SelectVisibleProgram(preferredProgram);
        ApplyItemFilter();
        SelectVisibleItem(preferredItem);
    }

    private IEnumerable<AppEngineItem> GetVisibleItems()
    {
        if (!_isGlobalSearchMode)
        {
            return _allItems;
        }

        return _globalSearchResults
            .Select(match => FindExistingItem(match.Item))
            .Where(item => item is not null)
            .Cast<AppEngineItem>()
            .Distinct(AppEngineItemIdentityComparer.Instance);
    }

    private AppEngineItem? FindExistingItem(AppEngineItem searchItem)
    {
        return _allItems.FirstOrDefault(existingItem => ItemsMatch(existingItem, searchItem));
    }

    private void UpdateGlobalSearchChrome()
    {
        bool hasActiveSearchMode = _isGlobalSearchMode;
        ClearGlobalSearchButton.Visibility = hasActiveSearchMode ? Visibility.Visible : Visibility.Collapsed;
        ClearGlobalSearchButton.IsEnabled = hasActiveSearchMode;
    }

    private void SelectVisibleProgram(string? preferredProgram)
    {
        string? nextProgram = _filteredPrograms.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(preferredProgram) &&
            _filteredPrograms.Contains(preferredProgram, StringComparer.OrdinalIgnoreCase))
        {
            nextProgram = preferredProgram;
        }

        if (!string.Equals(ProgramsListView.SelectedItem as string, nextProgram, StringComparison.OrdinalIgnoreCase))
        {
            ProgramsListView.SelectedItem = nextProgram;
        }
    }

    private void SelectVisibleItem(AppEngineItem? preferredItem)
    {
        AppEngineItem? nextItem = _filteredItems.FirstOrDefault();
        if (preferredItem is not null)
        {
            AppEngineItem? matchingItem = _filteredItems.FirstOrDefault(item => ItemsMatch(item, preferredItem));
            if (matchingItem is not null)
            {
                nextItem = matchingItem;
            }
        }

        if (!ReferenceEquals(ItemsListView.SelectedItem, nextItem))
        {
            ItemsListView.SelectedItem = nextItem;
        }
    }

    private void SetGlobalSearchStatus(string text, bool isVisible)
    {
        GlobalSearchStatusTextBlock.Text = text;
        GlobalSearchStatusTextBlock.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SetMetadata(AppEngineItem? item)
    {
        if (item is null)
        {
            SelectedItemTitleTextBlock.Text = string.Empty;
            SelectedItemSubtitleTextBlock.Text = string.Empty;
            MetadataSummaryTextBlock.Text =
                "Select an App Engine item to view its source. Current browsing assumes OBJECTVALUE1..7 map to Program / Section / Market / DB Type / EffDt / Step / Action for OBJECTID1 = 66.";
            return;
        }

        SelectedItemTitleTextBlock.Text = item.DisplayName;
        SelectedItemSubtitleTextBlock.Text = item.ProgramName;
        MetadataSummaryTextBlock.Text = BuildMetadataText(item);
    }

    private string BuildMetadataText(AppEngineItem item)
    {
        return
            $"PROGRAM={ValueOrPlaceholder(item.ProgramName)}, SECTION={ValueOrPlaceholder(item.SectionName)}, STEP={ValueOrPlaceholder(item.StepName)}, ACTION={ValueOrPlaceholder(item.ActionName)}, MARKET={ValueOrPlaceholder(item.Market)}, DBTYPE={ValueOrPlaceholder(item.DatabaseType)}, EFFDT={ValueOrPlaceholder(item.EffectiveDateKey)}, LASTUPDOPRID={ValueOrPlaceholder(item.LastUpdatedBy)}, LASTUPDDTTM={item.LastUpdatedDateTime?.ToString("u") ?? "(blank)"}";
    }

    private void SetSourceViewerText(string text)
    {
        _currentSourceText = text ?? string.Empty;
        _currentSourceMatchRanges = _isGlobalSearchMode && !string.IsNullOrWhiteSpace(_activeGlobalSearchText)
            ? PeopleCodeSourceFormatter.GetMatchRanges(_currentSourceText, _activeGlobalSearchText)
            : Array.Empty<TextRange>();
        _activeSourceMatchIndex = _currentSourceMatchRanges.Count > 0 ? 0 : -1;

        PeopleCodeSourceFormatter.ApplyFormatting(
            SourceRichTextBlock,
            _currentSourceText,
            useSyntaxHighlighting: true,
            _isGlobalSearchMode ? _activeGlobalSearchText : null,
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

    private void RefreshSourceViewerFormatting()
    {
        _currentSourceMatchRanges = _isGlobalSearchMode && !string.IsNullOrWhiteSpace(_activeGlobalSearchText)
            ? PeopleCodeSourceFormatter.GetMatchRanges(_currentSourceText, _activeGlobalSearchText)
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
            _currentSourceText,
            useSyntaxHighlighting: true,
            _isGlobalSearchMode ? _activeGlobalSearchText : null,
            _activeSourceMatchIndex);

        UpdateSourceMatchChrome();
    }

    private void UpdateSourceMatchChrome()
    {
        bool hasNavigableMatches = _isGlobalSearchMode && _currentSourceMatchRanges.Count > 0;
        PreviousSourceMatchButton.IsEnabled = hasNavigableMatches;
        NextSourceMatchButton.IsEnabled = hasNavigableMatches;

        SourceMatchStatusTextBlock.Text = hasNavigableMatches
            ? $"Match {_activeSourceMatchIndex + 1} of {_currentSourceMatchRanges.Count}"
            : _isGlobalSearchMode && !string.IsNullOrWhiteSpace(_activeGlobalSearchText)
                ? "No matches in current source"
                : string.Empty;
    }

    private void NavigateCurrentSourceMatch(int direction)
    {
        if (!_isGlobalSearchMode || _currentSourceMatchRanges.Count == 0)
        {
            return;
        }

        int nextIndex = _activeSourceMatchIndex;
        if (nextIndex < 0)
        {
            nextIndex = 0;
        }
        else
        {
            nextIndex = (nextIndex + direction + _currentSourceMatchRanges.Count) % _currentSourceMatchRanges.Count;
        }

        _activeSourceMatchIndex = nextIndex;
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
        int upperBound = Math.Min(activeRange.StartIndex, _currentSourceText.Length);
        for (int index = 0; index < upperBound; index++)
        {
            if (_currentSourceText[index] == '\n')
            {
                precedingLineBreaks++;
                lastLineBreakIndex = index;
            }
        }

        int columnIndex = Math.Max(0, upperBound - lastLineBreakIndex - 1);
        string[] sourceLines = _currentSourceText.Split('\n');
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

    private static bool ItemMatchesSearch(AppEngineItem item, string searchText)
    {
        return item.ProgramName.Contains(searchText, StringComparison.OrdinalIgnoreCase)
            || item.SectionName.Contains(searchText, StringComparison.OrdinalIgnoreCase)
            || item.StepName.Contains(searchText, StringComparison.OrdinalIgnoreCase)
            || item.ActionName.Contains(searchText, StringComparison.OrdinalIgnoreCase)
            || item.Market.Contains(searchText, StringComparison.OrdinalIgnoreCase)
            || item.DatabaseType.Contains(searchText, StringComparison.OrdinalIgnoreCase)
            || item.EffectiveDateKey.Contains(searchText, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ItemsMatch(AppEngineItem left, AppEngineItem right)
    {
        return left.ProgramName.Equals(right.ProgramName, StringComparison.OrdinalIgnoreCase)
            && left.SectionName.Equals(right.SectionName, StringComparison.OrdinalIgnoreCase)
            && left.Market.Equals(right.Market, StringComparison.OrdinalIgnoreCase)
            && left.DatabaseType.Equals(right.DatabaseType, StringComparison.OrdinalIgnoreCase)
            && left.EffectiveDateKey.Equals(right.EffectiveDateKey, StringComparison.OrdinalIgnoreCase)
            && left.StepName.Equals(right.StepName, StringComparison.OrdinalIgnoreCase)
            && left.ActionName.Equals(right.ActionName, StringComparison.OrdinalIgnoreCase);
    }

    private static string ValueOrPlaceholder(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "(blank)" : value;
    }

    private sealed class AppEngineItemIdentityComparer : IEqualityComparer<AppEngineItem>
    {
        public static AppEngineItemIdentityComparer Instance { get; } = new();

        public bool Equals(AppEngineItem? x, AppEngineItem? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null || y is null)
            {
                return false;
            }

            return ItemsMatch(x, y);
        }

        public int GetHashCode(AppEngineItem obj)
        {
            return HashCode.Combine(
                obj.ProgramName.ToUpperInvariant(),
                obj.SectionName.ToUpperInvariant(),
                obj.Market.ToUpperInvariant(),
                obj.DatabaseType.ToUpperInvariant(),
                obj.EffectiveDateKey.ToUpperInvariant(),
                obj.StepName.ToUpperInvariant(),
                obj.ActionName.ToUpperInvariant());
        }
    }
}
