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

public sealed partial class ComponentPeopleCodeBrowserView : UserControl
{
    private const int GlobalSearchResultLimit = 200;

    private readonly ComponentPeopleCodeBrowserService _browserService = new();
    private readonly List<ComponentPeopleCodeItem> _allItems = [];
    private readonly ObservableCollection<ComponentPeopleCodeComponentKey> _filteredComponents = [];
    private readonly ObservableCollection<ComponentPeopleCodeItem> _filteredItems = [];
    private readonly ObservableCollection<ComponentPeopleCodeSourceSearchMatch> _globalSearchResults = [];

    private OracleConnectionSession? _session;
    private PeopleCodeObjectStatusStore? _statusStore;
    private ComponentPeopleCodeItem? _selectedItem;
    private int _globalSearchVersion;
    private int _sourceLoadVersion;
    private bool _isGlobalSearchMode;
    private string _activeGlobalSearchText = string.Empty;
    private string _currentSourceText = string.Empty;
    private IReadOnlyList<TextRange> _currentSourceMatchRanges = Array.Empty<TextRange>();
    private int _activeSourceMatchIndex = -1;

    public ComponentPeopleCodeBrowserView()
    {
        InitializeComponent();
        ComponentsListView.ItemsSource = _filteredComponents;
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
        _statusStore?.SetSessionAvailable(AllObjectsPeopleCodeBrowserService.ComponentMode, hasSession: true);
        _ = LoadItemsAsync();
    }

    public void SetStatusStore(PeopleCodeObjectStatusStore statusStore)
    {
        _statusStore = statusStore;
    }

    public Task RefreshAsync()
    {
        return LoadItemsAsync();
    }

    public async Task<bool> OpenItemAsync(ComponentPeopleCodeItem item)
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

        ComponentPeopleCodeItem? match = _allItems.FirstOrDefault(candidate => ItemsMatch(candidate, item));
        if (match is null)
        {
            return false;
        }

        ComponentPeopleCodeComponentKey key = new()
        {
            ComponentName = match.ComponentName,
            Market = match.Market
        };

        ComponentsListView.SelectedItem = _filteredComponents.FirstOrDefault(component =>
            ComponentKeyComparer.Instance.Equals(component, key));
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

    private void ComponentSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyComponentFilter();
    }

    private void ItemSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyItemFilter();
    }

    private void ComponentsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplyItemFilter();
    }

    private async void ItemsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ItemsListView.SelectedItem is not ComponentPeopleCodeItem item)
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
        MetadataSummaryTextBlock.Text = "Loading Component PeopleCode source...";

        ComponentPeopleCodeSourceResult result = await _browserService.GetSourceAsync(_session.Options, item);
        if (sourceLoadVersion != _sourceLoadVersion || !ReferenceEquals(_selectedItem, item))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            InlineErrorInfoBar.Message = result.ErrorMessage;
            InlineErrorInfoBar.IsOpen = true;
            MetadataSummaryTextBlock.Text = "Component PeopleCode source could not be loaded.";
            SetSourceViewerText(string.Empty);
            return;
        }

        MetadataSummaryTextBlock.Text = string.IsNullOrWhiteSpace(result.SourceText)
            ? item.BuildMetadataSummary() + " No source rows were returned for the selected key."
            : item.BuildMetadataSummary();

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

        _statusStore?.MarkLoading(AllObjectsPeopleCodeBrowserService.ComponentMode);
        InlineErrorInfoBar.IsOpen = false;
        GlobalSearchErrorInfoBar.IsOpen = false;
        _allItems.Clear();
        _filteredComponents.Clear();
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
        MetadataSummaryTextBlock.Text = "Loading Component PeopleCode metadata...";

        ComponentPeopleCodeBrowseResult result = await _browserService.GetItemsAsync(_session.Options);
        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            _statusStore?.MarkError(AllObjectsPeopleCodeBrowserService.ComponentMode);
            MetadataSummaryTextBlock.Text = "Component PeopleCode metadata could not be loaded.";
            InlineErrorInfoBar.Message = result.ErrorMessage;
            InlineErrorInfoBar.IsOpen = true;
            return;
        }

        _allItems.AddRange(result.Items);
        ApplyComponentFilter();
        SelectVisibleComponent(null);
        ApplyItemFilter();
        SelectVisibleItem(null);

        if (_allItems.Count == 0)
        {
            MetadataSummaryTextBlock.Text =
                "No Component PeopleCode rows were returned for the verified read-only subset. Current Component mode reads PSPCMTXT/PSPCMPROG rows where OBJECTID1=10, OBJECTID2=39, OBJECTID3=1, OBJECTID4=12, and OBJECTID5-7 are 0, mapping OBJECTVALUE1-4 to Component / Market / Item / Event.";
        }

        _statusStore?.MarkLoaded(AllObjectsPeopleCodeBrowserService.ComponentMode);
    }

    private void ApplyComponentFilter()
    {
        ComponentPeopleCodeComponentKey? previouslySelectedComponent = ComponentsListView.SelectedItem as ComponentPeopleCodeComponentKey;
        string searchText = ComponentSearchTextBox.Text.Trim();

        _filteredComponents.Clear();

        IEnumerable<ComponentPeopleCodeComponentKey> components = GetVisibleItems()
            .Select(item => new ComponentPeopleCodeComponentKey
            {
                ComponentName = item.ComponentName,
                Market = item.Market
            })
            .Distinct(ComponentKeyComparer.Instance)
            .OrderBy(component => component.ComponentName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(component => component.Market, StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            components = components.Where(component => component.SearchSummary.Contains(searchText, StringComparison.OrdinalIgnoreCase));
        }

        foreach (ComponentPeopleCodeComponentKey component in components)
        {
            _filteredComponents.Add(component);
        }

        NoComponentsTextBlock.Text = _isGlobalSearchMode
            ? "No components match the current global PeopleCode search."
            : "No components found.";
        NoComponentsTextBlock.Visibility = _filteredComponents.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        SelectVisibleComponent(previouslySelectedComponent);
        ApplyItemFilter();
    }

    private void ApplyItemFilter()
    {
        ComponentPeopleCodeComponentKey? selectedComponent = ComponentsListView.SelectedItem as ComponentPeopleCodeComponentKey;
        ComponentPeopleCodeItem? preferredItem = _selectedItem;
        string searchText = ItemSearchTextBox.Text.Trim();

        _filteredItems.Clear();

        IEnumerable<ComponentPeopleCodeItem> items = GetVisibleItems();
        if (selectedComponent is not null)
        {
            items = items.Where(item =>
                item.ComponentName.Equals(selectedComponent.ComponentName, StringComparison.OrdinalIgnoreCase) &&
                item.Market.Equals(selectedComponent.Market, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            items = [];
        }

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            items = items.Where(item => ItemMatchesSearch(item, searchText));
        }

        foreach (ComponentPeopleCodeItem item in items)
        {
            _filteredItems.Add(item);
        }

        NoItemsTextBlock.Text = _isGlobalSearchMode
            ? "No matching items found for the selected component."
            : "No items found.";
        NoItemsTextBlock.Visibility =
            selectedComponent is not null && _filteredItems.Count == 0
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

        SetGlobalSearchStatus("Searching Component PeopleCode...", true);
        GlobalSourceSearchButton.IsEnabled = false;
        UpdateGlobalSearchChrome();

        ComponentPeopleCodeSourceSearchResult result = await _browserService.SearchSourceAsync(
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

        foreach (ComponentPeopleCodeSourceSearchMatch match in result.Matches)
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

        ComponentPeopleCodeComponentKey? preferredComponent = ComponentsListView.SelectedItem as ComponentPeopleCodeComponentKey;
        ComponentPeopleCodeItem? preferredItem = _selectedItem;

        ApplyComponentFilter();
        SelectVisibleComponent(preferredComponent);
        ApplyItemFilter();
        SelectVisibleItem(preferredItem);

        int componentCount = _globalSearchResults
            .Select(match => new ComponentPeopleCodeComponentKey
            {
                ComponentName = match.Item.ComponentName,
                Market = match.Item.Market
            })
            .Distinct(ComponentKeyComparer.Instance)
            .Count();

        string countMessage = _globalSearchResults.Count == 0
            ? $"Global search mode is active for \"{searchText}\". No matching items were found."
            : $"Global search mode is active for \"{searchText}\". {_globalSearchResults.Count} matching item(s) across {componentCount} component-market pair(s).";
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

        ComponentPeopleCodeComponentKey? preferredComponent = ComponentsListView.SelectedItem as ComponentPeopleCodeComponentKey;
        ComponentPeopleCodeItem? preferredItem = _selectedItem;

        ApplyComponentFilter();
        SelectVisibleComponent(preferredComponent);
        ApplyItemFilter();
        SelectVisibleItem(preferredItem);
    }

    private IEnumerable<ComponentPeopleCodeItem> GetVisibleItems()
    {
        if (!_isGlobalSearchMode)
        {
            return _allItems;
        }

        return _globalSearchResults
            .Select(match => FindExistingItem(match.Item))
            .Where(item => item is not null)
            .Cast<ComponentPeopleCodeItem>()
            .Distinct(ComponentPeopleCodeItemIdentityComparer.Instance);
    }

    private ComponentPeopleCodeItem? FindExistingItem(ComponentPeopleCodeItem searchItem)
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

    private void SelectVisibleComponent(ComponentPeopleCodeComponentKey? preferredComponent)
    {
        ComponentPeopleCodeComponentKey? nextComponent = _filteredComponents.FirstOrDefault();
        if (preferredComponent is not null)
        {
            ComponentPeopleCodeComponentKey? matchingComponent = _filteredComponents.FirstOrDefault(component =>
                ComponentKeyComparer.Instance.Equals(component, preferredComponent));
            if (matchingComponent is not null)
            {
                nextComponent = matchingComponent;
            }
        }

        if (!ReferenceEquals(ComponentsListView.SelectedItem, nextComponent))
        {
            ComponentsListView.SelectedItem = nextComponent;
        }
    }

    private void SelectVisibleItem(ComponentPeopleCodeItem? preferredItem)
    {
        ComponentPeopleCodeItem? nextItem = _filteredItems.FirstOrDefault();
        if (preferredItem is not null)
        {
            ComponentPeopleCodeItem? matchingItem = _filteredItems.FirstOrDefault(item => ItemsMatch(item, preferredItem));
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

    private void SetMetadata(ComponentPeopleCodeItem? item)
    {
        if (item is null)
        {
            SelectedItemTitleTextBlock.Text = string.Empty;
            SelectedItemSubtitleTextBlock.Text = string.Empty;
            MetadataSummaryTextBlock.Text =
                "Select a Component PeopleCode item to view its source. Current Component mode reads the verified PSPCMTXT/PSPCMPROG subset where OBJECTID1=10, OBJECTID2=39, OBJECTID3=1, OBJECTID4=12, and OBJECTID5-7 are 0, mapping OBJECTVALUE1-4 to Component / Market / Item / Event.";
            return;
        }

        SelectedItemTitleTextBlock.Text = item.DisplayName;
        SelectedItemSubtitleTextBlock.Text = $"{item.ComponentName} | {item.Market} | {item.StructureLabel}";
        MetadataSummaryTextBlock.Text = item.BuildMetadataSummary();
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

    private static bool ItemMatchesSearch(ComponentPeopleCodeItem item, string searchText)
    {
        return item.SearchSummary.Contains(searchText, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ItemsMatch(ComponentPeopleCodeItem left, ComponentPeopleCodeItem right)
    {
        return left.ObjectId2 == right.ObjectId2
            && left.ObjectId3 == right.ObjectId3
            && left.ObjectId4 == right.ObjectId4
            && left.ObjectId5 == right.ObjectId5
            && left.ObjectId6 == right.ObjectId6
            && left.ObjectId7 == right.ObjectId7
            && left.ComponentName.Equals(right.ComponentName, StringComparison.OrdinalIgnoreCase)
            && left.Market.Equals(right.Market, StringComparison.OrdinalIgnoreCase)
            && left.ItemName.Equals(right.ItemName, StringComparison.OrdinalIgnoreCase)
            && left.EventName.Equals(right.EventName, StringComparison.OrdinalIgnoreCase)
            && left.ObjectValue5.Equals(right.ObjectValue5, StringComparison.OrdinalIgnoreCase)
            && left.ObjectValue6.Equals(right.ObjectValue6, StringComparison.OrdinalIgnoreCase)
            && left.ObjectValue7.Equals(right.ObjectValue7, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ComponentKeyComparer : IEqualityComparer<ComponentPeopleCodeComponentKey>
    {
        public static ComponentKeyComparer Instance { get; } = new();

        public bool Equals(ComponentPeopleCodeComponentKey? x, ComponentPeopleCodeComponentKey? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null || y is null)
            {
                return false;
            }

            return x.ComponentName.Equals(y.ComponentName, StringComparison.OrdinalIgnoreCase)
                && x.Market.Equals(y.Market, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode(ComponentPeopleCodeComponentKey obj)
        {
            return HashCode.Combine(
                obj.ComponentName.ToUpperInvariant(),
                obj.Market.ToUpperInvariant());
        }
    }

    private sealed class ComponentPeopleCodeItemIdentityComparer : IEqualityComparer<ComponentPeopleCodeItem>
    {
        public static ComponentPeopleCodeItemIdentityComparer Instance { get; } = new();

        public bool Equals(ComponentPeopleCodeItem? x, ComponentPeopleCodeItem? y)
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

        public int GetHashCode(ComponentPeopleCodeItem obj)
        {
            HashCode hash = new();
            hash.Add(obj.ObjectId2);
            hash.Add(obj.ObjectId3);
            hash.Add(obj.ObjectId4);
            hash.Add(obj.ObjectId5);
            hash.Add(obj.ObjectId6);
            hash.Add(obj.ObjectId7);
            hash.Add(obj.ComponentName, StringComparer.OrdinalIgnoreCase);
            hash.Add(obj.Market, StringComparer.OrdinalIgnoreCase);
            hash.Add(obj.ItemName, StringComparer.OrdinalIgnoreCase);
            hash.Add(obj.EventName, StringComparer.OrdinalIgnoreCase);
            hash.Add(obj.ObjectValue5, StringComparer.OrdinalIgnoreCase);
            hash.Add(obj.ObjectValue6, StringComparer.OrdinalIgnoreCase);
            hash.Add(obj.ObjectValue7, StringComparer.OrdinalIgnoreCase);
            return hash.ToHashCode();
        }
    }
}
