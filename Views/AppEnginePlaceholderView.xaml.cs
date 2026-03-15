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
    private readonly PeopleCodeAuthoringCapabilityService _authoringCapabilityService = new();
    private readonly PeopleSoftUserNameResolverService _userNameResolver = new();
    private readonly DetachedSourceWindowManager _detachedSourceWindowManager;
    private readonly PeopleCodeCompareWindowManager _compareWindowManager;
    private readonly List<AppEngineItem> _allItems = [];
    private readonly ObservableCollection<string> _filteredPrograms = [];
    private readonly ObservableCollection<AppEngineItem> _filteredItems = [];
    private readonly ObservableCollection<AppEngineSourceSearchMatch> _globalSearchResults = [];

    private OracleConnectionSession? _session;
    private PeopleCodeObjectStatusStore? _statusStore;
    private AppEngineItem? _selectedItem;
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

    public AppEnginePlaceholderView(
        DetachedSourceWindowManager detachedSourceWindowManager,
        PeopleCodeCompareWindowManager compareWindowManager)
    {
        _detachedSourceWindowManager = detachedSourceWindowManager;
        _compareWindowManager = compareWindowManager;
        InitializeComponent();
        MetadataHeaderView.OpenButton.Click += OpenDetachedSourceButton_Click;
        MetadataHeaderView.CompareButton.Click += CompareSourceButton_Click;
        ProgramsListView.ItemsSource = _filteredPrograms;
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
        _statusStore?.SetSessionAvailable(AllObjectsPeopleCodeBrowserService.AppEngineMode, hasSession: true);
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

    public async Task<bool> OpenItemAsync(AppEngineItem item)
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

        AppEngineItem? match = _allItems.FirstOrDefault(candidate => ItemsMatch(candidate, item));
        if (match is null)
        {
            return false;
        }

        ProgramsListView.SelectedItem = _filteredPrograms.FirstOrDefault(program =>
            program.Equals(match.ProgramName, StringComparison.OrdinalIgnoreCase));
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
        MetadataHeaderView.SetKeysText("Loading App Engine source...", "Details");

        AppEngineSourceResult result = await _browserService.GetSourceAsync(_session.Options, item);
        if (sourceLoadVersion != _sourceLoadVersion || !ReferenceEquals(_selectedItem, item))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            InlineErrorInfoBar.Message = result.ErrorMessage;
            InlineErrorInfoBar.IsOpen = true;
            MetadataHeaderView.SetKeysText("App Engine source could not be loaded.", "Details");
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

        _statusStore?.MarkLoading(AllObjectsPeopleCodeBrowserService.AppEngineMode);
        InlineErrorInfoBar.IsOpen = false;
        GlobalSearchErrorInfoBar.IsOpen = false;
        _allItems.Clear();
        _filteredPrograms.Clear();
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
        MetadataHeaderView.SetKeysText("Loading App Engine metadata...", "Details");

        AppEngineBrowseResult result = await _browserService.GetItemsAsync(session.Options);
        if (loadItemsVersion != _loadItemsVersion ||
            _session is null ||
            !_session.ProfileId.Equals(session.ProfileId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            _statusStore?.MarkError(AllObjectsPeopleCodeBrowserService.AppEngineMode);
            MetadataHeaderView.SetKeysText("App Engine metadata could not be loaded.", "Details");
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
            MetadataHeaderView.SetKeysText(
                "No App Engine PeopleCode rows were returned for the current read-only subset. This browser assumes OBJECTVALUE1..7 map to Program / Section / Market / DB Type / EffDt / Step / Action for OBJECTID1 = 66.",
                "Details");
        }

        _statusStore?.MarkLoaded(AllObjectsPeopleCodeBrowserService.AppEngineMode);
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
        int metadataVersion = ++_metadataVersion;

        if (item is null)
        {
            MetadataHeaderView.SetTitle(string.Empty);
            MetadataHeaderView.SetTypeText(string.Empty);
            MetadataHeaderView.SetUpdatedText(string.Empty);
            MetadataHeaderView.SetKeysText(
                "Select an App Engine item to view its source. Current browsing assumes OBJECTVALUE1..7 map to Program / Section / Market / DB Type / EffDt / Step / Action for OBJECTID1 = 66.",
                "Details");
            return;
        }

        MetadataHeaderView.SetTitle(item.DisplayName);
        MetadataHeaderView.SetTypeText(BuildTypeText(item));
        MetadataHeaderView.SetKeysText(BuildMetadataText(item));
        MetadataHeaderView.SetUpdatedText(BuildLastUpdatedText(item.LastUpdatedBy, item.LastUpdatedDateTime));
        _ = UpdateMetadataLastUpdatedAsync(item, metadataVersion);
    }

    private string BuildMetadataText(AppEngineItem item)
    {
        return JoinMetadataParts(
            ("PROGRAM", item.ProgramName),
            ("SECTION", item.SectionName),
            ("STEP", item.StepName),
            ("ACTION", item.ActionName),
            ("MARKET", item.Market),
            ("DBTYPE", item.DatabaseType),
            ("EFFDT", item.EffectiveDateKey));
    }

    private async Task UpdateMetadataLastUpdatedAsync(AppEngineItem item, int metadataVersion)
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

    private static string BuildTypeText(AppEngineItem item)
    {
        return string.IsNullOrWhiteSpace(item.ActionName) ? "App Engine Step" : "App Engine Step Action";
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
            "App Engine",
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
            ObjectType = AllObjectsPeopleCodeBrowserService.AppEngineMode,
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
                    ObjectType = AllObjectsPeopleCodeBrowserService.AppEngineMode,
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
