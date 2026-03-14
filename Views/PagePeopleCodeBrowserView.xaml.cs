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

public sealed partial class PagePeopleCodeBrowserView : UserControl
{
    private const int GlobalSearchResultLimit = 200;

    private readonly PagePeopleCodeBrowserService _browserService = new();
    private readonly PeopleSoftUserNameResolverService _userNameResolver = new();
    private readonly DetachedSourceWindowManager _detachedSourceWindowManager;
    private readonly PeopleCodeCompareWindowManager _compareWindowManager;
    private readonly List<PagePeopleCodeItem> _allItems = [];
    private readonly ObservableCollection<string> _filteredPages = [];
    private readonly ObservableCollection<PagePeopleCodeItem> _filteredItems = [];
    private readonly ObservableCollection<PagePeopleCodeSourceSearchMatch> _globalSearchResults = [];

    private OracleConnectionSession? _session;
    private PeopleCodeObjectStatusStore? _statusStore;
    private PagePeopleCodeItem? _selectedItem;
    private int _globalSearchVersion;
    private int _sourceLoadVersion;
    private int _loadItemsVersion;
    private int _metadataVersion;
    private bool _isGlobalSearchMode;
    private string _activeGlobalSearchText = string.Empty;
    private string _currentSourceText = string.Empty;
    private bool _hasLoadedSelectedSource;
    private IReadOnlyList<TextRange> _currentSourceMatchRanges = Array.Empty<TextRange>();
    private int _activeSourceMatchIndex = -1;

    public PagePeopleCodeBrowserView(
        DetachedSourceWindowManager detachedSourceWindowManager,
        PeopleCodeCompareWindowManager compareWindowManager)
    {
        _detachedSourceWindowManager = detachedSourceWindowManager;
        _compareWindowManager = compareWindowManager;
        InitializeComponent();
        PagesListView.ItemsSource = _filteredPages;
        ItemsListView.ItemsSource = _filteredItems;
        SourceRichTextBlock.Blocks.Add(new Paragraph());
        SetMetadata(null);
        UpdateGlobalSearchChrome();
        UpdateSourceMatchChrome();
    }

    public void SetSession(OracleConnectionSession session)
    {
        _session = session;
        RefreshButton.IsEnabled = true;
        GlobalSourceSearchButton.IsEnabled = true;
        _statusStore?.SetSessionAvailable(AllObjectsPeopleCodeBrowserService.PageMode, hasSession: true);
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

    public async Task<bool> OpenItemAsync(PagePeopleCodeItem item)
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

        PagePeopleCodeItem? match = _allItems.FirstOrDefault(candidate => ItemsMatch(candidate, item));
        if (match is null)
        {
            return false;
        }

        PagesListView.SelectedItem = _filteredPages.FirstOrDefault(page =>
            page.Equals(match.PageName, StringComparison.OrdinalIgnoreCase));
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

    private void PageSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyPageFilter();
    }

    private void ItemSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyItemFilter();
    }

    private void PagesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplyItemFilter();
    }

    private async void ItemsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ItemsListView.SelectedItem is not PagePeopleCodeItem item)
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
        MetadataSummaryTextBlock.Text = "Loading Page PeopleCode source...";

        PagePeopleCodeSourceResult result = await _browserService.GetSourceAsync(_session.Options, item);
        if (sourceLoadVersion != _sourceLoadVersion || !ReferenceEquals(_selectedItem, item))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            InlineErrorInfoBar.Message = result.ErrorMessage;
            InlineErrorInfoBar.IsOpen = true;
            MetadataSummaryTextBlock.Text = "Page PeopleCode source could not be loaded.";
            SetSourceViewerText(string.Empty);
            return;
        }

        MetadataSummaryTextBlock.Text = string.IsNullOrWhiteSpace(result.SourceText)
            ? BuildMetadataText(item) + " No source rows were returned for the selected key."
            : BuildMetadataText(item);

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

        _statusStore?.MarkLoading(AllObjectsPeopleCodeBrowserService.PageMode);
        InlineErrorInfoBar.IsOpen = false;
        GlobalSearchErrorInfoBar.IsOpen = false;
        _allItems.Clear();
        _filteredPages.Clear();
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
        MetadataSummaryTextBlock.Text = "Loading Page PeopleCode metadata...";

        PagePeopleCodeBrowseResult result = await _browserService.GetItemsAsync(session.Options);
        if (loadItemsVersion != _loadItemsVersion ||
            _session is null ||
            !_session.ProfileId.Equals(session.ProfileId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            _statusStore?.MarkError(AllObjectsPeopleCodeBrowserService.PageMode);
            MetadataSummaryTextBlock.Text = "Page PeopleCode metadata could not be loaded.";
            InlineErrorInfoBar.Message = result.ErrorMessage;
            InlineErrorInfoBar.IsOpen = true;
            return;
        }

        _allItems.AddRange(result.Items);
        ApplyPageFilter();
        SelectVisiblePage(null);
        ApplyItemFilter();
        SelectVisibleItem(null);

        if (_allItems.Count == 0)
        {
            MetadataSummaryTextBlock.Text =
                "No Page PeopleCode rows were returned for the current read-only subset. This browser currently reads page-scoped PSPCMTXT/PSPCMPROG rows where OBJECTID1=9, labels common Page Event and Page Record Field Event shapes, and leaves other page keys undecoded instead of assuming unsupported mappings.";
        }

        _statusStore?.MarkLoaded(AllObjectsPeopleCodeBrowserService.PageMode);
    }

    private void ApplyPageFilter()
    {
        string? previouslySelectedPage = PagesListView.SelectedItem as string;
        string searchText = PageSearchTextBox.Text.Trim();

        _filteredPages.Clear();

        IEnumerable<string> pages = GetVisibleItems()
            .Select(item => item.PageName)
            .Where(pageName => !string.IsNullOrWhiteSpace(pageName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(pageName => pageName, StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            pages = pages.Where(pageName =>
                pageName.Contains(searchText, StringComparison.OrdinalIgnoreCase));
        }

        foreach (string page in pages)
        {
            _filteredPages.Add(page);
        }

        NoPagesTextBlock.Text = _isGlobalSearchMode
            ? "No pages match the current global PeopleCode search."
            : "No pages found.";
        NoPagesTextBlock.Visibility = _filteredPages.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        SelectVisiblePage(previouslySelectedPage);
        ApplyItemFilter();
    }

    private void ApplyItemFilter()
    {
        string? selectedPage = PagesListView.SelectedItem as string;
        PagePeopleCodeItem? preferredItem = _selectedItem;
        string searchText = ItemSearchTextBox.Text.Trim();

        _filteredItems.Clear();

        IEnumerable<PagePeopleCodeItem> items = GetVisibleItems();
        if (!string.IsNullOrWhiteSpace(selectedPage))
        {
            items = items.Where(item =>
                item.PageName.Equals(selectedPage, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            items = [];
        }

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            items = items.Where(item => ItemMatchesSearch(item, searchText));
        }

        foreach (PagePeopleCodeItem item in items)
        {
            _filteredItems.Add(item);
        }

        NoItemsTextBlock.Text = _isGlobalSearchMode
            ? "No matching items found for the selected page."
            : "No items found.";
        NoItemsTextBlock.Visibility =
            !string.IsNullOrWhiteSpace(selectedPage) && _filteredItems.Count == 0
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

        SetGlobalSearchStatus("Searching Page PeopleCode...", true);
        GlobalSourceSearchButton.IsEnabled = false;
        UpdateGlobalSearchChrome();

        PagePeopleCodeSourceSearchResult result = await _browserService.SearchSourceAsync(
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

        foreach (PagePeopleCodeSourceSearchMatch match in result.Matches)
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

        string? preferredPage = _selectedItem?.PageName;
        PagePeopleCodeItem? preferredItem = _selectedItem;

        ApplyPageFilter();
        SelectVisiblePage(preferredPage);
        ApplyItemFilter();
        SelectVisibleItem(preferredItem);

        int pageCount = _globalSearchResults
            .Select(match => match.Item.PageName)
            .Where(pageName => !string.IsNullOrWhiteSpace(pageName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        string countMessage = _globalSearchResults.Count == 0
            ? $"Global search mode is active for \"{searchText}\". No matching items were found."
            : $"Global search mode is active for \"{searchText}\". {_globalSearchResults.Count} matching item(s) across {pageCount} page(s).";
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

        string? preferredPage = _selectedItem?.PageName;
        PagePeopleCodeItem? preferredItem = _selectedItem;

        ApplyPageFilter();
        SelectVisiblePage(preferredPage);
        ApplyItemFilter();
        SelectVisibleItem(preferredItem);
    }

    private IEnumerable<PagePeopleCodeItem> GetVisibleItems()
    {
        if (!_isGlobalSearchMode)
        {
            return _allItems;
        }

        return _globalSearchResults
            .Select(match => FindExistingItem(match.Item))
            .Where(item => item is not null)
            .Cast<PagePeopleCodeItem>()
            .Distinct(PagePeopleCodeItemIdentityComparer.Instance);
    }

    private PagePeopleCodeItem? FindExistingItem(PagePeopleCodeItem searchItem)
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

    private void SelectVisiblePage(string? preferredPage)
    {
        string? nextPage = _filteredPages.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(preferredPage) &&
            _filteredPages.Contains(preferredPage, StringComparer.OrdinalIgnoreCase))
        {
            nextPage = preferredPage;
        }

        if (!string.Equals(PagesListView.SelectedItem as string, nextPage, StringComparison.OrdinalIgnoreCase))
        {
            PagesListView.SelectedItem = nextPage;
        }
    }

    private void SelectVisibleItem(PagePeopleCodeItem? preferredItem)
    {
        PagePeopleCodeItem? nextItem = _filteredItems.FirstOrDefault();
        if (preferredItem is not null)
        {
            PagePeopleCodeItem? matchingItem = _filteredItems.FirstOrDefault(item => ItemsMatch(item, preferredItem));
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

    private void SetMetadata(PagePeopleCodeItem? item)
    {
        int metadataVersion = ++_metadataVersion;

        if (item is null)
        {
            MetadataLastUpdatedTextBlock.Text = string.Empty;
            SelectedItemTitleTextBlock.Text = string.Empty;
            SelectedItemSubtitleTextBlock.Text = string.Empty;
            MetadataSummaryTextBlock.Text =
                "Select a Page PeopleCode item to view its source. Current Page mode reads PSPCMTXT/PSPCMPROG rows where OBJECTID1=9, explicitly labels common Page Event and Page Record Field Event shapes, and shows raw key metadata when a page-scoped structure is not yet decoded.";
            return;
        }

        SelectedItemTitleTextBlock.Text = item.DisplayName;
        SelectedItemSubtitleTextBlock.Text = $"{item.PageName} | {item.StructureLabel}";
        MetadataSummaryTextBlock.Text = BuildMetadataText(item);
        MetadataLastUpdatedTextBlock.Text = BuildLastUpdatedText(item.LastUpdatedBy, item.LastUpdatedDateTime);
        _ = UpdateMetadataLastUpdatedAsync(item, metadataVersion);
    }

    private static string BuildMetadataText(PagePeopleCodeItem item)
    {
        return JoinMetadataParts(
            ("PAGE", item.PageName),
            ("STRUCTURE", item.StructureLabel),
            ("OBJECTIDS", BuildObjectIdLabel(item)),
            ("OBJECTVALUES", BuildObjectValueLabel(item)));
    }

    private async Task UpdateMetadataLastUpdatedAsync(PagePeopleCodeItem item, int metadataVersion)
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

        MetadataLastUpdatedTextBlock.Text = BuildLastUpdatedText(displayLabel, item.LastUpdatedDateTime);
    }

    private static string BuildLastUpdatedText(string displayLabel, DateTime? lastUpdatedDateTime)
    {
        if (string.IsNullOrWhiteSpace(displayLabel) && lastUpdatedDateTime is null)
        {
            return string.Empty;
        }

        string updatedBy = string.IsNullOrWhiteSpace(displayLabel) ? "(blank)" : displayLabel;
        string updatedOn = FormatLastUpdatedDateTime(lastUpdatedDateTime);
        return $"Last updated by {updatedBy} on {updatedOn}";
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

    private static bool ItemMatchesSearch(PagePeopleCodeItem item, string searchText)
    {
        return item.SearchSummary.Contains(searchText, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ItemsMatch(PagePeopleCodeItem left, PagePeopleCodeItem right)
    {
        return left.PageName.Equals(right.PageName, StringComparison.OrdinalIgnoreCase)
            && left.ObjectId2 == right.ObjectId2
            && left.ObjectId3 == right.ObjectId3
            && left.ObjectId4 == right.ObjectId4
            && left.ObjectId5 == right.ObjectId5
            && left.ObjectId6 == right.ObjectId6
            && left.ObjectId7 == right.ObjectId7
            && left.ObjectValue2.Equals(right.ObjectValue2, StringComparison.OrdinalIgnoreCase)
            && left.ObjectValue3.Equals(right.ObjectValue3, StringComparison.OrdinalIgnoreCase)
            && left.ObjectValue4.Equals(right.ObjectValue4, StringComparison.OrdinalIgnoreCase)
            && left.ObjectValue5.Equals(right.ObjectValue5, StringComparison.OrdinalIgnoreCase)
            && left.ObjectValue6.Equals(right.ObjectValue6, StringComparison.OrdinalIgnoreCase)
            && left.ObjectValue7.Equals(right.ObjectValue7, StringComparison.OrdinalIgnoreCase);
    }

    private static string ValueOrPlaceholder(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "(blank)" : value;
    }

    private static string ValueOrPlaceholder(int? value)
    {
        return value?.ToString() ?? "(blank)";
    }

    private static string BuildObjectIdLabel(PagePeopleCodeItem item)
    {
        return string.Join(
            "/",
            new int?[]
            {
                item.ObjectId2,
                item.ObjectId3,
                item.ObjectId4,
                item.ObjectId5,
                item.ObjectId6,
                item.ObjectId7
            }
            .Where(value => value is not null)
            .Select(value => value!.Value.ToString()));
    }

    private static string BuildObjectValueLabel(PagePeopleCodeItem item)
    {
        return string.Join(
            "/",
            new[]
            {
                item.ObjectValue2,
                item.ObjectValue3,
                item.ObjectValue4,
                item.ObjectValue5,
                item.ObjectValue6,
                item.ObjectValue7
            }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
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
        OpenDetachedSourceButton.IsEnabled = CanOpenDetachedSource();
        UpdateCompareChrome();
    }

    private DetachedPeopleCodeSourceContext BuildDetachedSourceContext()
    {
        return DetachedPeopleCodeSourceContextFactory.Create(
            _session,
            "Page",
            SelectedItemTitleTextBlock.Text,
            SelectedItemSubtitleTextBlock.Text,
            MetadataSummaryTextBlock.Text,
            MetadataLastUpdatedTextBlock.Text,
            _currentSourceText,
            _isGlobalSearchMode ? _activeGlobalSearchText : null,
            useSyntaxHighlighting: true);
    }

    private bool CanCompareSource()
    {
        return _compareWindowManager.CanCompare(_session, _selectedItem is not null && _hasLoadedSelectedSource);
    }

    private void UpdateCompareChrome()
    {
        CompareSourceButton.IsEnabled = CanCompareSource();
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
                    ObjectType = AllObjectsPeopleCodeBrowserService.PageMode,
                    SourceKey = _selectedItem
                },
                ObjectTitle = SelectedItemTitleTextBlock.Text,
                ObjectSubtitle = SelectedItemSubtitleTextBlock.Text,
                MetadataSummary = MetadataSummaryTextBlock.Text,
                UseSyntaxHighlighting = true
            }
        };
    }

    private sealed class PagePeopleCodeItemIdentityComparer : IEqualityComparer<PagePeopleCodeItem>
    {
        public static PagePeopleCodeItemIdentityComparer Instance { get; } = new();

        public bool Equals(PagePeopleCodeItem? x, PagePeopleCodeItem? y)
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

        public int GetHashCode(PagePeopleCodeItem obj)
        {
            HashCode hash = new();
            hash.Add(obj.PageName, StringComparer.OrdinalIgnoreCase);
            hash.Add(obj.ObjectId2);
            hash.Add(obj.ObjectId3);
            hash.Add(obj.ObjectId4);
            hash.Add(obj.ObjectId5);
            hash.Add(obj.ObjectId6);
            hash.Add(obj.ObjectId7);
            hash.Add(obj.ObjectValue2, StringComparer.OrdinalIgnoreCase);
            hash.Add(obj.ObjectValue3, StringComparer.OrdinalIgnoreCase);
            hash.Add(obj.ObjectValue4, StringComparer.OrdinalIgnoreCase);
            hash.Add(obj.ObjectValue5, StringComparer.OrdinalIgnoreCase);
            hash.Add(obj.ObjectValue6, StringComparer.OrdinalIgnoreCase);
            hash.Add(obj.ObjectValue7, StringComparer.OrdinalIgnoreCase);
            return hash.ToHashCode();
        }
    }
}
