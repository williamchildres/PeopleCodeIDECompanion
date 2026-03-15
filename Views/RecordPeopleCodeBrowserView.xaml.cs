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

public sealed partial class RecordPeopleCodeBrowserView : UserControl
{
    private const int GlobalSearchResultLimit = 200;

    private readonly RecordPeopleCodeBrowserService _browserService = new();
    private readonly PeopleCodeAuthoringCapabilityService _authoringCapabilityService = new();
    private readonly PeopleSoftUserNameResolverService _userNameResolver = new();
    private readonly DetachedSourceWindowManager _detachedSourceWindowManager;
    private readonly PeopleCodeCompareWindowManager _compareWindowManager;
    private readonly List<RecordPeopleCodeItem> _allItems = [];
    private readonly ObservableCollection<string> _filteredRecords = [];
    private readonly ObservableCollection<RecordPeopleCodeItem> _filteredItems = [];
    private readonly ObservableCollection<RecordPeopleCodeSourceSearchMatch> _globalSearchResults = [];

    private OracleConnectionSession? _session;
    private PeopleCodeObjectStatusStore? _statusStore;
    private RecordPeopleCodeItem? _selectedItem;
    private int _globalSearchVersion;
    private int _sourceLoadVersion;
    private int _loadItemsVersion;
    private int _metadataVersion;
    private bool _isGlobalSearchMode;
    private string _activeGlobalSearchText = string.Empty;
    private string _currentSourceText = string.Empty;
    private PeopleCodeAuthoringCapabilitySnapshot _authoringCapabilities = new();
    private bool _hasLoadedSelectedSource;
    private IReadOnlyList<TextRange> _currentSourceMatchRanges = Array.Empty<TextRange>();
    private int _activeSourceMatchIndex = -1;

    public RecordPeopleCodeBrowserView(
        DetachedSourceWindowManager detachedSourceWindowManager,
        PeopleCodeCompareWindowManager compareWindowManager)
    {
        _detachedSourceWindowManager = detachedSourceWindowManager;
        _compareWindowManager = compareWindowManager;
        InitializeComponent();
        MetadataHeaderView.OpenButton.Click += OpenDetachedSourceButton_Click;
        MetadataHeaderView.CompareButton.Click += CompareSourceButton_Click;
        RecordsListView.ItemsSource = _filteredRecords;
        ItemsListView.ItemsSource = _filteredItems;
        SourceRichTextBlock.Blocks.Add(new Paragraph());
        SetMetadata(null);
        UpdateGlobalSearchChrome();
        UpdateSourceMatchChrome();
        UpdateAuthoringChrome();
    }

    public void SetSession(OracleConnectionSession session)
    {
        _session = session;
        GlobalSourceSearchButton.IsEnabled = true;
        _statusStore?.SetSessionAvailable(AllObjectsPeopleCodeBrowserService.RecordMode, hasSession: true);
        _ = LoadAuthoringCapabilitiesAsync();
        _ = LoadItemsAsync();
    }

    public void SetStatusStore(PeopleCodeObjectStatusStore statusStore)
    {
        _statusStore = statusStore;
    }

    public void FocusGlobalSearch()
    {
        GlobalSourceSearchTextBox.Focus(FocusState.Programmatic);
        GlobalSourceSearchTextBox.SelectAll();
    }

    public Task RefreshAsync()
    {
        return LoadItemsAsync();
    }

    public async Task<bool> OpenItemAsync(RecordPeopleCodeItem item)
    {
        if (_session is null)
        {
            return false;
        }

        if (_allItems.Count == 0)
        {
            await LoadItemsAsync();
        }

        if (_isGlobalSearchMode)
        {
            ClearGlobalSearchMode();
        }

        RecordPeopleCodeItem? match = _allItems.FirstOrDefault(candidate => ItemsMatch(candidate, item));
        if (match is null)
        {
            return false;
        }

        RecordsListView.SelectedItem = _filteredRecords.FirstOrDefault(record =>
            record.Equals(match.RecordName, StringComparison.OrdinalIgnoreCase));
        ApplyItemFilter();
        ItemsListView.SelectedItem = _filteredItems.FirstOrDefault(candidate => ItemsMatch(candidate, match));
        if (ItemsListView.SelectedItem is not null)
        {
            ItemsListView.ScrollIntoView(ItemsListView.SelectedItem);
        }

        return ItemsListView.SelectedItem is not null;
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadItemsAsync();
    }

    private void RecordSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyRecordFilter();
    }

    private void ItemSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyItemFilter();
    }

    private void RecordsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplyItemFilter();
    }

    private async void ItemsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ItemsListView.SelectedItem is not RecordPeopleCodeItem item)
        {
            _selectedItem = null;
            _sourceLoadVersion++;
            _hasLoadedSelectedSource = false;
            SetMetadata(null);
            SetSourceViewerText(string.Empty);
            return;
        }

        _selectedItem = item;
        _hasLoadedSelectedSource = false;
        UpdateDetachedSourceChrome();
        SetMetadata(item);

        if (_session is null)
        {
            return;
        }

        int sourceLoadVersion = ++_sourceLoadVersion;
        MetadataHeaderView.SetKeysText("Loading Record PeopleCode source...", "Details");

        RecordPeopleCodeSourceResult result = await _browserService.GetSourceAsync(_session.Options, item);
        if (sourceLoadVersion != _sourceLoadVersion || !ReferenceEquals(_selectedItem, item))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            InlineErrorInfoBar.Message = result.ErrorMessage;
            InlineErrorInfoBar.IsOpen = true;
            MetadataHeaderView.SetKeysText("Record PeopleCode source could not be loaded.", "Details");
            SetSourceViewerText(string.Empty);
            return;
        }

        MetadataHeaderView.SetKeysText(string.IsNullOrWhiteSpace(result.SourceText)
            ? BuildMetadataText(item) + " No source rows were returned for the selected key."
            : BuildMetadataText(item));

        _hasLoadedSelectedSource = true;
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

    private void OpenDetachedSourceButton_Click(object sender, RoutedEventArgs e)
    {
        if (!CanOpenDetachedSource())
        {
            return;
        }

        _detachedSourceWindowManager.Open(BuildDetachedSourceContext());
    }

    private void CompareSourceButton_Click(object sender, RoutedEventArgs e)
    {
        if (!CanCompareSource() || sender is not FrameworkElement anchor || _session is null)
        {
            return;
        }

        IReadOnlyList<OracleConnectionSession> comparisonProfiles =
            _compareWindowManager.GetAvailableComparisonProfiles(_session);
        if (comparisonProfiles.Count == 0)
        {
            UpdateCompareChrome();
            return;
        }

        MenuFlyout flyout = new();
        foreach (OracleConnectionSession comparisonProfile in comparisonProfiles)
        {
            MenuFlyoutItem menuItem = new()
            {
                Text = comparisonProfile.DisplayName
            };
            menuItem.Click += async (_, _) =>
            {
                PeopleCodeCompareRequest? request = BuildCompareRequest(comparisonProfile);
                if (request is not null)
                {
                    await _compareWindowManager.OpenAsync(request);
                }
            };
            flyout.Items.Add(menuItem);
        }

        flyout.ShowAt(anchor);
    }

    private async Task LoadItemsAsync()
    {
        if (_session is null)
        {
            return;
        }

        OracleConnectionSession session = _session;
        int loadItemsVersion = ++_loadItemsVersion;

        _statusStore?.MarkLoading(AllObjectsPeopleCodeBrowserService.RecordMode);
        InlineErrorInfoBar.IsOpen = false;
        GlobalSearchErrorInfoBar.IsOpen = false;
        _allItems.Clear();
        _filteredRecords.Clear();
        _filteredItems.Clear();
        _globalSearchResults.Clear();
        _selectedItem = null;
        _isGlobalSearchMode = false;
        _activeGlobalSearchText = string.Empty;
        _hasLoadedSelectedSource = false;
        GlobalSourceSearchTextBox.Text = string.Empty;
        SetGlobalSearchStatus(string.Empty, false);
        SetMetadata(null);
        SetSourceViewerText(string.Empty);
        UpdateGlobalSearchChrome();
        MetadataHeaderView.SetKeysText("Loading Record PeopleCode metadata...", "Details");

        RecordPeopleCodeBrowseResult result = await _browserService.GetItemsAsync(session.Options);
        if (loadItemsVersion != _loadItemsVersion ||
            _session is null ||
            !_session.ProfileId.Equals(session.ProfileId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            _statusStore?.MarkError(AllObjectsPeopleCodeBrowserService.RecordMode);
            MetadataHeaderView.SetKeysText("Record PeopleCode metadata could not be loaded.", "Details");
            InlineErrorInfoBar.Message = result.ErrorMessage;
            InlineErrorInfoBar.IsOpen = true;
            return;
        }

        _allItems.AddRange(result.Items);
        ApplyRecordFilter();
        SelectVisibleRecord(null);
        ApplyItemFilter();
        SelectVisibleItem(null);

        SetGlobalSearchStatus(
            $"Loaded {_allItems.Count} Record item(s) in {result.LoadDuration.TotalSeconds:F1}s."
            + (string.IsNullOrWhiteSpace(result.WarningMessage) ? string.Empty : $" {result.WarningMessage}"),
            true);

        if (_allItems.Count == 0)
        {
            MetadataHeaderView.SetKeysText(
                "No Record PeopleCode rows were returned for the current read-only subset. This browser currently reads field-event PeopleCode from PSPCMTXT/PSPCMPROG where OBJECTID1=1, OBJECTID2=2, and OBJECTID3=12.",
                "Details");
        }

        _statusStore?.MarkLoaded(AllObjectsPeopleCodeBrowserService.RecordMode);
    }

    private void ApplyRecordFilter()
    {
        string? previouslySelectedRecord = RecordsListView.SelectedItem as string;
        string searchText = RecordSearchTextBox.Text.Trim();

        _filteredRecords.Clear();

        IEnumerable<string> records = GetVisibleItems()
            .Select(item => item.RecordName)
            .Where(recordName => !string.IsNullOrWhiteSpace(recordName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(recordName => recordName, StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            records = records.Where(recordName =>
                recordName.Contains(searchText, StringComparison.OrdinalIgnoreCase));
        }

        foreach (string record in records)
        {
            _filteredRecords.Add(record);
        }

        NoRecordsTextBlock.Text = _isGlobalSearchMode
            ? "No records match the current global PeopleCode search."
            : "No records found.";
        NoRecordsTextBlock.Visibility = _filteredRecords.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        SelectVisibleRecord(previouslySelectedRecord);
        ApplyItemFilter();
    }

    private void ApplyItemFilter()
    {
        string? selectedRecord = RecordsListView.SelectedItem as string;
        RecordPeopleCodeItem? preferredItem = _selectedItem;
        string searchText = ItemSearchTextBox.Text.Trim();

        _filteredItems.Clear();

        IEnumerable<RecordPeopleCodeItem> items = GetVisibleItems();
        if (!string.IsNullOrWhiteSpace(selectedRecord))
        {
            items = items.Where(item =>
                item.RecordName.Equals(selectedRecord, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            items = [];
        }

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            items = items.Where(item => ItemMatchesSearch(item, searchText));
        }

        foreach (RecordPeopleCodeItem item in items)
        {
            _filteredItems.Add(item);
        }

        NoItemsTextBlock.Text = _isGlobalSearchMode
            ? "No matching items found for the selected record."
            : "No items found.";
        NoItemsTextBlock.Visibility =
            !string.IsNullOrWhiteSpace(selectedRecord) && _filteredItems.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;

        SelectVisibleItem(preferredItem);

        if (_filteredItems.Count == 0)
        {
            _selectedItem = null;
            _sourceLoadVersion++;
            _hasLoadedSelectedSource = false;
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

        SetGlobalSearchStatus("Searching Record PeopleCode...", true);
        GlobalSourceSearchButton.IsEnabled = false;
        UpdateGlobalSearchChrome();

        RecordPeopleCodeSourceSearchResult result = await _browserService.SearchSourceAsync(
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

        foreach (RecordPeopleCodeSourceSearchMatch match in result.Matches)
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

        string? preferredRecord = _selectedItem?.RecordName;
        RecordPeopleCodeItem? preferredItem = _selectedItem;

        ApplyRecordFilter();
        SelectVisibleRecord(preferredRecord);
        ApplyItemFilter();
        SelectVisibleItem(preferredItem);

        int recordCount = _globalSearchResults
            .Select(match => match.Item.RecordName)
            .Where(recordName => !string.IsNullOrWhiteSpace(recordName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        string countMessage = _globalSearchResults.Count == 0
            ? $"Global search mode is active for \"{searchText}\". No matching items were found."
            : $"Global search mode is active for \"{searchText}\". {_globalSearchResults.Count} matching item(s) across {recordCount} record(s).";
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

        string? preferredRecord = _selectedItem?.RecordName;
        RecordPeopleCodeItem? preferredItem = _selectedItem;

        ApplyRecordFilter();
        SelectVisibleRecord(preferredRecord);
        ApplyItemFilter();
        SelectVisibleItem(preferredItem);
    }

    private IEnumerable<RecordPeopleCodeItem> GetVisibleItems()
    {
        if (!_isGlobalSearchMode)
        {
            return _allItems;
        }

        return _globalSearchResults
            .Select(match => FindExistingItem(match.Item))
            .Where(item => item is not null)
            .Cast<RecordPeopleCodeItem>()
            .Distinct(RecordPeopleCodeItemIdentityComparer.Instance);
    }

    private RecordPeopleCodeItem? FindExistingItem(RecordPeopleCodeItem searchItem)
    {
        return _allItems.FirstOrDefault(existingItem => ItemsMatch(existingItem, searchItem));
    }

    private void UpdateGlobalSearchChrome()
    {
        bool hasActiveSearchMode = _isGlobalSearchMode;
        ClearGlobalSearchButton.Visibility = hasActiveSearchMode ? Visibility.Visible : Visibility.Collapsed;
        ClearGlobalSearchButton.IsEnabled = hasActiveSearchMode;
        GlobalSearchLimitationTextBlock.Visibility = Visibility.Visible;
    }

    private void SelectVisibleRecord(string? preferredRecord)
    {
        string? nextRecord = _filteredRecords.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(preferredRecord) &&
            _filteredRecords.Contains(preferredRecord, StringComparer.OrdinalIgnoreCase))
        {
            nextRecord = preferredRecord;
        }

        if (!string.Equals(RecordsListView.SelectedItem as string, nextRecord, StringComparison.OrdinalIgnoreCase))
        {
            RecordsListView.SelectedItem = nextRecord;
        }
    }

    private void SelectVisibleItem(RecordPeopleCodeItem? preferredItem)
    {
        RecordPeopleCodeItem? nextItem = _filteredItems.FirstOrDefault();
        if (preferredItem is not null)
        {
            RecordPeopleCodeItem? matchingItem = _filteredItems.FirstOrDefault(item => ItemsMatch(item, preferredItem));
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

    private void SetMetadata(RecordPeopleCodeItem? item)
    {
        int metadataVersion = ++_metadataVersion;

        if (item is null)
        {
            MetadataHeaderView.SetTitle(string.Empty);
            MetadataHeaderView.SetTypeText(string.Empty);
            MetadataHeaderView.SetUpdatedText(string.Empty);
            MetadataHeaderView.SetKeysText(
                "Select a Record PeopleCode item to view its source. Current Record mode browses field-event PeopleCode from PSPCMTXT/PSPCMPROG where OBJECTID1=1, OBJECTID2=2, and OBJECTID3=12.",
                "Details");
            return;
        }

        MetadataHeaderView.SetTitle(item.DisplayName);
        MetadataHeaderView.SetTypeText(item.LevelLabel);
        MetadataHeaderView.SetKeysText(BuildMetadataText(item));
        MetadataHeaderView.SetUpdatedText(BuildLastUpdatedText(item.LastUpdatedBy, item.LastUpdatedDateTime));
        _ = UpdateMetadataLastUpdatedAsync(item, metadataVersion);
    }

    private string BuildMetadataText(RecordPeopleCodeItem item)
    {
        return JoinMetadataParts(
            ("RECORD", item.RecordName),
            ("FIELD", item.FieldName),
            ("EVENT", item.EventName),
            ("LEVEL", item.LevelLabel));
    }

    private async Task UpdateMetadataLastUpdatedAsync(RecordPeopleCodeItem item, int metadataVersion)
    {
        if (_session is null || string.IsNullOrWhiteSpace(item.LastUpdatedBy) || !item.LastUpdatedBy.All(char.IsDigit))
        {
            return;
        }

        string displayLabel = await _userNameResolver.GetDisplayLabelAsync(_session.Options, item.LastUpdatedBy);
        if (metadataVersion != _metadataVersion || !ReferenceEquals(_selectedItem, item))
        {
            return;
        }

        MetadataHeaderView.SetUpdatedText(BuildLastUpdatedText(displayLabel, item.LastUpdatedDateTime));
    }

    private static string BuildLastUpdatedText(string displayLabel, DateTime? lastUpdatedDateTime)
    {
        if (string.IsNullOrWhiteSpace(displayLabel) && lastUpdatedDateTime is null)
        {
            return string.Empty;
        }

        string updatedBy = string.IsNullOrWhiteSpace(displayLabel) ? "(blank)" : displayLabel;
        string updatedOn = FormatLastUpdatedDateTime(lastUpdatedDateTime);
        return $"{updatedBy} · {updatedOn}";
    }

    private static string FormatLastUpdatedDateTime(DateTime? lastUpdatedDateTime)
    {
        if (lastUpdatedDateTime is null)
        {
            return "(blank)";
        }

        DateTime displayDateTime = lastUpdatedDateTime.Value.Kind == DateTimeKind.Utc
            ? lastUpdatedDateTime.Value.ToLocalTime()
            : lastUpdatedDateTime.Value;
        return displayDateTime.ToString("M/d/yyyy h:mm tt");
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

        UpdateDetachedSourceChrome();
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
        bool hasActiveSearch = _isGlobalSearchMode && !string.IsNullOrWhiteSpace(_activeGlobalSearchText);
        bool hasNavigableMatches = hasActiveSearch && _currentSourceMatchRanges.Count > 0;
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

    private static bool ItemMatchesSearch(RecordPeopleCodeItem item, string searchText)
    {
        return item.RecordName.Contains(searchText, StringComparison.OrdinalIgnoreCase)
            || item.FieldName.Contains(searchText, StringComparison.OrdinalIgnoreCase)
            || item.EventName.Contains(searchText, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ItemsMatch(RecordPeopleCodeItem left, RecordPeopleCodeItem right)
    {
        return left.RecordName.Equals(right.RecordName, StringComparison.OrdinalIgnoreCase)
            && left.FieldName.Equals(right.FieldName, StringComparison.OrdinalIgnoreCase)
            && left.EventName.Equals(right.EventName, StringComparison.OrdinalIgnoreCase);
    }

    private static string ValueOrPlaceholder(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "(blank)" : value;
    }

    private static string JoinMetadataParts(params (string Label, string? Value)[] parts)
    {
        return string.Join(
            ", ",
            parts
                .Where(part => !string.IsNullOrWhiteSpace(part.Value))
                .Select(part => $"{part.Label}={part.Value}"));
    }

    private bool CanOpenDetachedSource()
    {
        return _selectedItem is not null && _hasLoadedSelectedSource;
    }

    private void UpdateDetachedSourceChrome()
    {
        MetadataHeaderView.OpenButton.IsEnabled = CanOpenDetachedSource();
        UpdateAuthoringChrome();
        UpdateCompareChrome();
    }

    private DetachedPeopleCodeSourceContext BuildDetachedSourceContext()
    {
        return DetachedPeopleCodeSourceContextFactory.Create(
            _session,
            "Record",
            MetadataHeaderView.TitleText,
            MetadataHeaderView.TypeValueText,
            MetadataHeaderView.KeysValueText,
            MetadataHeaderView.UpdatedValueText,
            _currentSourceText,
            _isGlobalSearchMode ? _activeGlobalSearchText : null,
            useSyntaxHighlighting: true,
            sourceIdentity: BuildSourceIdentity(),
            authoringCapabilities: _authoringCapabilities);
    }

    private bool CanCompareSource()
    {
        return _compareWindowManager.CanCompare(_session, _selectedItem is not null && _hasLoadedSelectedSource);
    }

    private void UpdateCompareChrome()
    {
        MetadataHeaderView.CompareButton.IsEnabled = CanCompareSource();
    }

    private async Task LoadAuthoringCapabilitiesAsync()
    {
        _authoringCapabilities = await _authoringCapabilityService.GetCurrentAsync();
        UpdateAuthoringChrome();
    }

    private void UpdateAuthoringChrome()
    {
        MetadataHeaderView.SetAuthoringState(
            _authoringCapabilityService.CreatePresentationState(
                _authoringCapabilities,
                _selectedItem is not null ? BuildSourceIdentity() : null,
                _selectedItem is not null && _hasLoadedSelectedSource));
    }

    private PeopleCodeSourceIdentity BuildSourceIdentity()
    {
        return new PeopleCodeSourceIdentity
        {
            ProfileId = _session?.ProfileId ?? string.Empty,
            ObjectType = AllObjectsPeopleCodeBrowserService.RecordMode,
            ObjectTitle = MetadataHeaderView.TitleText,
            SourceKey = _selectedItem ?? new object()
        };
    }

    private PeopleCodeCompareRequest? BuildCompareRequest(OracleConnectionSession comparisonProfile)
    {
        if (_session is null || _selectedItem is null || !_hasLoadedSelectedSource)
        {
            return null;
        }

        return new PeopleCodeCompareRequest
        {
            LeftSession = _session,
            RightSession = comparisonProfile,
            LeftSourceText = _currentSourceText,
            SourceDescriptor = new PeopleCodeSourceDescriptor
            {
                Identity = new PeopleCodeSourceIdentity
                {
                    ProfileId = _session.ProfileId,
                    ObjectType = AllObjectsPeopleCodeBrowserService.RecordMode,
                    ObjectTitle = MetadataHeaderView.TitleText,
                    SourceKey = _selectedItem
                },
                ObjectTitle = MetadataHeaderView.TitleText,
                ObjectSubtitle = MetadataHeaderView.TypeValueText,
                MetadataSummary = MetadataHeaderView.KeysValueText,
                UseSyntaxHighlighting = true
            }
        };
    }

    private sealed class RecordPeopleCodeItemIdentityComparer : IEqualityComparer<RecordPeopleCodeItem>
    {
        public static RecordPeopleCodeItemIdentityComparer Instance { get; } = new();

        public bool Equals(RecordPeopleCodeItem? x, RecordPeopleCodeItem? y)
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

        public int GetHashCode(RecordPeopleCodeItem obj)
        {
            return HashCode.Combine(
                obj.RecordName.ToUpperInvariant(),
                obj.FieldName.ToUpperInvariant(),
                obj.EventName.ToUpperInvariant());
        }
    }
}
