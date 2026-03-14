using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using PeopleCodeIDECompanion.Models;
using PeopleCodeIDECompanion.Services;
using Windows.Foundation;
using Windows.System;

namespace PeopleCodeIDECompanion.Views;

public sealed class AppPackageBrowserView : UserControl
{
    private const int GlobalSearchResultLimit = 200;

    private readonly AppPackageBrowserService _browserService = new();
    private readonly List<string> _allPackageRoots = [];
    private readonly List<AppPackageEntry> _allEntries = [];
    private readonly ObservableCollection<AppPackageEntry> _filteredEntries = [];
    private readonly ObservableCollection<string> _filteredPackageRoots = [];
    private readonly ObservableCollection<AppPackageSourceSearchMatch> _globalSearchResults = [];

    private readonly TextBlock _connectionSummaryTextBlock;
    private readonly Button _refreshButton;
    private readonly InfoBar _inlineErrorInfoBar;
    private readonly TextBox _packageSearchTextBox;
    private readonly TextBlock _noPackagesTextBlock;
    private readonly ListView _packageRootsListView;
    private readonly TextBox _entrySearchTextBox;
    private readonly TextBlock _noEntriesTextBlock;
    private readonly ListView _entriesListView;
    private readonly TextBox _globalSourceSearchTextBox;
    private readonly Button _globalSourceSearchButton;
    private readonly Button _clearGlobalSearchButton;
    private readonly InfoBar _globalSearchErrorInfoBar;
    private readonly TextBlock _globalSearchStatusTextBlock;
    private readonly TextBlock _globalSearchLimitationTextBlock;
    private readonly TextBlock _selectedEntryTitleTextBlock;
    private readonly TextBlock _selectedEntryTypeTextBlock;
    private readonly TextBlock _metadataSummaryTextBlock;
    private readonly Button _previousSourceMatchButton;
    private readonly Button _nextSourceMatchButton;
    private readonly TextBlock _sourceMatchStatusTextBlock;
    private readonly RichTextBlock _sourceRichTextBlock;
    private readonly ScrollViewer _sourceScrollViewer;

    private OracleConnectionSession? _session;
    private AppPackageEntry? _selectedEntry;
    private int _globalSearchVersion;
    private int _sourceLoadVersion;
    private bool _isGlobalSearchMode;
    private string _activeGlobalSearchText = string.Empty;
    private string _currentSourceText = string.Empty;
    private bool _currentSourceUsesSyntaxHighlighting;
    private IReadOnlyList<TextRange> _currentSourceMatchRanges = Array.Empty<TextRange>();
    private int _activeSourceMatchIndex = -1;

    public AppPackageBrowserView()
    {
        _connectionSummaryTextBlock = new TextBlock
        {
            Text = "Connect from the Oracle Connection screen to load App Package PeopleCode.",
            TextWrapping = TextWrapping.WrapWholeWords
        };

        _refreshButton = new Button
        {
            Content = "Refresh",
            IsEnabled = false
        };
        _refreshButton.Click += RefreshButton_Click;

        _inlineErrorInfoBar = new InfoBar
        {
            IsClosable = false,
            IsOpen = false,
            Severity = InfoBarSeverity.Error
        };

        _packageSearchTextBox = new TextBox
        {
            PlaceholderText = "Search packages"
        };
        _packageSearchTextBox.TextChanged += PackageSearchTextBox_TextChanged;

        _noPackagesTextBlock = BuildEmptyStateTextBlock("No packages found.");

        _packageRootsListView = new ListView
        {
            ItemsSource = _filteredPackageRoots
        };
        ScrollViewer.SetVerticalScrollBarVisibility(_packageRootsListView, ScrollBarVisibility.Auto);
        ScrollViewer.SetHorizontalScrollBarVisibility(_packageRootsListView, ScrollBarVisibility.Disabled);
        _packageRootsListView.SelectionChanged += PackageRootsListView_SelectionChanged;

        _entrySearchTextBox = new TextBox
        {
            PlaceholderText = "Search entries"
        };
        _entrySearchTextBox.TextChanged += EntrySearchTextBox_TextChanged;

        _noEntriesTextBlock = BuildEmptyStateTextBlock("No entries found.");

        _entriesListView = new ListView
        {
            ItemsSource = _filteredEntries,
            ItemTemplate = BuildEntryTemplate()
        };
        ScrollViewer.SetVerticalScrollBarVisibility(_entriesListView, ScrollBarVisibility.Auto);
        ScrollViewer.SetHorizontalScrollBarVisibility(_entriesListView, ScrollBarVisibility.Disabled);
        _entriesListView.SelectionChanged += EntriesListView_SelectionChanged;

        _globalSourceSearchTextBox = new TextBox
        {
            PlaceholderText = "Search PeopleCode source text across App Packages"
        };
        _globalSourceSearchTextBox.KeyDown += GlobalSourceSearchTextBox_KeyDown;

        _globalSourceSearchButton = new Button
        {
            Content = "Search",
            IsEnabled = false
        };
        _globalSourceSearchButton.Click += GlobalSourceSearchButton_Click;

        _clearGlobalSearchButton = new Button
        {
            Content = "Clear",
            IsEnabled = false,
            Visibility = Visibility.Collapsed
        };
        _clearGlobalSearchButton.Click += ClearGlobalSearchButton_Click;

        _globalSearchErrorInfoBar = new InfoBar
        {
            IsClosable = false,
            IsOpen = false,
            Severity = InfoBarSeverity.Error
        };

        _globalSearchStatusTextBlock = BuildEmptyStateTextBlock(string.Empty);

        _globalSearchLimitationTextBlock = new TextBlock
        {
            Text = $"Matches are case-insensitive, limited to the first {GlobalSearchResultLimit} matching entries, and currently match within stored source rows rather than across row boundaries.",
            Foreground = Application.Current.Resources["TextFillColorSecondaryBrush"] as Brush,
            TextWrapping = TextWrapping.WrapWholeWords
        };

        _selectedEntryTitleTextBlock = new TextBlock
        {
            TextWrapping = TextWrapping.WrapWholeWords
        };
        _selectedEntryTitleTextBlock.Style = Application.Current.Resources["BodyStrongTextBlockStyle"] as Style;

        _selectedEntryTypeTextBlock = new TextBlock();

        _metadataSummaryTextBlock = new TextBlock
        {
            TextWrapping = TextWrapping.WrapWholeWords
        };

        _previousSourceMatchButton = new Button
        {
            Content = "Previous Match",
            IsEnabled = false
        };
        _previousSourceMatchButton.Click += PreviousSourceMatchButton_Click;

        _nextSourceMatchButton = new Button
        {
            Content = "Next Match",
            IsEnabled = false
        };
        _nextSourceMatchButton.Click += NextSourceMatchButton_Click;

        _sourceMatchStatusTextBlock = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Application.Current.Resources["TextFillColorSecondaryBrush"] as Brush
        };

        _sourceRichTextBlock = new RichTextBlock
        {
            FontFamily = new FontFamily("Consolas"),
            IsTextSelectionEnabled = true,
            MinHeight = 300,
            TextWrapping = TextWrapping.NoWrap,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _sourceRichTextBlock.Blocks.Add(new Paragraph());

        _sourceScrollViewer = new ScrollViewer
        {
            Content = _sourceRichTextBlock,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        Content = BuildLayout();
        SetGlobalSearchStatus(string.Empty, false);
        SetMetadata(null);
        UpdateGlobalSearchChrome();
        UpdateSourceMatchChrome();
    }

    public void SetSession(OracleConnectionSession session)
    {
        _session = session;
        _connectionSummaryTextBlock.Text = string.IsNullOrWhiteSpace(session.DisplayName)
            ? $"Connected as {session.Options.Username} to {session.Options.Host}:{session.Options.Port}/{session.Options.ServiceName}"
            : $"Connected with profile {session.DisplayName} as {session.Options.Username} to {session.Options.Host}:{session.Options.Port}/{session.Options.ServiceName}";
        _refreshButton.IsEnabled = true;
        _globalSourceSearchButton.IsEnabled = true;
        _ = LoadEntriesAsync();
    }

    private UIElement BuildLayout()
    {
        Grid root = new()
        {
            Padding = new Thickness(24),
            RowSpacing = 16,
            ColumnSpacing = 16
        };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        Grid headerGrid = new() { ColumnSpacing = 16 };
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        StackPanel titleStack = new() { Spacing = 6 };
        TextBlock title = new() { Text = "App Package Browser" };
        title.Style = Application.Current.Resources["TitleTextBlockStyle"] as Style;
        titleStack.Children.Add(title);
        titleStack.Children.Add(_connectionSummaryTextBlock);
        headerGrid.Children.Add(titleStack);
        Grid.SetColumn(_refreshButton, 1);
        headerGrid.Children.Add(_refreshButton);
        root.Children.Add(headerGrid);

        Grid.SetRow(_inlineErrorInfoBar, 1);
        root.Children.Add(_inlineErrorInfoBar);

        Grid contentGrid = new() { ColumnSpacing = 16 };
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(320) });
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(contentGrid, 2);
        root.Children.Add(contentGrid);

        contentGrid.Children.Add(BuildSectionBorder("Packages", _packageSearchTextBox, _packageRootsListView, _noPackagesTextBlock));
        Border entriesHost = BuildSectionBorder("Entries", _entrySearchTextBox, _entriesListView, _noEntriesTextBlock);
        Grid.SetColumn(entriesHost, 1);
        contentGrid.Children.Add(entriesHost);

        Grid detailGrid = new() { RowSpacing = 16 };
        detailGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        detailGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        detailGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(detailGrid, 2);
        contentGrid.Children.Add(detailGrid);

        detailGrid.Children.Add(BuildGlobalSearchBorder());

        StackPanel metadataPanel = new() { Spacing = 6 };
        TextBlock metadataTitle = new() { Text = "Metadata" };
        metadataTitle.Style = Application.Current.Resources["SubtitleTextBlockStyle"] as Style;
        metadataPanel.Children.Add(metadataTitle);
        metadataPanel.Children.Add(_selectedEntryTitleTextBlock);
        metadataPanel.Children.Add(_selectedEntryTypeTextBlock);
        metadataPanel.Children.Add(_metadataSummaryTextBlock);
        Border metadataBorder = BuildPlainBorder(metadataPanel);
        Grid.SetRow(metadataBorder, 1);
        detailGrid.Children.Add(metadataBorder);

        Grid sourceGrid = new() { RowSpacing = 8 };
        sourceGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        sourceGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid sourceHeaderGrid = new() { ColumnSpacing = 8 };
        sourceHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        sourceHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        sourceHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        sourceHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        TextBlock sourceTitle = new() { Text = "PeopleCode Source" };
        sourceTitle.Style = Application.Current.Resources["SubtitleTextBlockStyle"] as Style;
        sourceHeaderGrid.Children.Add(sourceTitle);
        Grid.SetColumn(_sourceMatchStatusTextBlock, 1);
        sourceHeaderGrid.Children.Add(_sourceMatchStatusTextBlock);
        Grid.SetColumn(_previousSourceMatchButton, 2);
        sourceHeaderGrid.Children.Add(_previousSourceMatchButton);
        Grid.SetColumn(_nextSourceMatchButton, 3);
        sourceHeaderGrid.Children.Add(_nextSourceMatchButton);
        sourceGrid.Children.Add(sourceHeaderGrid);
        Grid.SetRow(_sourceScrollViewer, 1);
        sourceGrid.Children.Add(_sourceScrollViewer);
        Border sourceBorder = BuildPlainBorder(sourceGrid);
        Grid.SetRow(sourceBorder, 2);
        detailGrid.Children.Add(sourceBorder);

        return root;
    }

    private Border BuildGlobalSearchBorder()
    {
        Grid searchGrid = new() { RowSpacing = 8, ColumnSpacing = 8 };
        searchGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        searchGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        searchGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        searchGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        searchGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        TextBlock titleBlock = new() { Text = "Global PeopleCode Search" };
        titleBlock.Style = Application.Current.Resources["SubtitleTextBlockStyle"] as Style;
        Grid.SetColumnSpan(titleBlock, 3);
        searchGrid.Children.Add(titleBlock);

        Grid.SetRow(_globalSourceSearchTextBox, 1);
        searchGrid.Children.Add(_globalSourceSearchTextBox);

        Grid.SetRow(_globalSourceSearchButton, 1);
        Grid.SetColumn(_globalSourceSearchButton, 1);
        searchGrid.Children.Add(_globalSourceSearchButton);

        Grid.SetRow(_clearGlobalSearchButton, 1);
        Grid.SetColumn(_clearGlobalSearchButton, 2);
        searchGrid.Children.Add(_clearGlobalSearchButton);

        Grid.SetRow(_globalSearchErrorInfoBar, 2);
        Grid.SetColumnSpan(_globalSearchErrorInfoBar, 3);
        searchGrid.Children.Add(_globalSearchErrorInfoBar);

        Grid.SetRow(_globalSearchStatusTextBlock, 3);
        Grid.SetColumnSpan(_globalSearchStatusTextBlock, 3);
        searchGrid.Children.Add(_globalSearchStatusTextBlock);

        Grid.SetRow(_globalSearchLimitationTextBlock, 4);
        Grid.SetColumnSpan(_globalSearchLimitationTextBlock, 3);
        searchGrid.Children.Add(_globalSearchLimitationTextBlock);

        return BuildPlainBorder(searchGrid);
    }

    private static Border BuildSectionBorder(string title, TextBox searchTextBox, ListView listView, TextBlock emptyStateTextBlock)
    {
        Grid sectionGrid = new() { RowSpacing = 8 };
        sectionGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        sectionGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        sectionGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        sectionGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        TextBlock titleBlock = new() { Text = title };
        titleBlock.Style = Application.Current.Resources["SubtitleTextBlockStyle"] as Style;
        sectionGrid.Children.Add(titleBlock);

        Grid.SetRow(searchTextBox, 1);
        sectionGrid.Children.Add(searchTextBox);

        Grid.SetRow(listView, 2);
        sectionGrid.Children.Add(listView);

        Grid.SetRow(emptyStateTextBlock, 3);
        sectionGrid.Children.Add(emptyStateTextBlock);

        Border border = BuildPlainBorder(sectionGrid);
        border.MinHeight = 420;
        return border;
    }

    private static TextBlock BuildEmptyStateTextBlock(string text)
    {
        return new TextBlock
        {
            Text = text,
            Visibility = Visibility.Collapsed,
            Foreground = Application.Current.Resources["TextFillColorSecondaryBrush"] as Brush,
            TextWrapping = TextWrapping.WrapWholeWords
        };
    }

    private static Border BuildPlainBorder(UIElement child)
    {
        return new Border
        {
            Background = Application.Current.Resources["ControlFillColorSecondaryBrush"] as Brush,
            BorderBrush = Application.Current.Resources["CardStrokeColorDefaultBrush"] as Brush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12),
            Child = child
        };
    }

    private static DataTemplate BuildEntryTemplate()
    {
        return (DataTemplate)XamlReader.Load("""
<DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
    <StackPanel Padding="8" Spacing="4">
        <TextBlock Text="{Binding DisplayName}" Style="{ThemeResource BodyStrongTextBlockStyle}" TextWrapping="WrapWholeWords" />
        <TextBlock Text="{Binding EntryType}" Foreground="{ThemeResource TextFillColorSecondaryBrush}" />
    </StackPanel>
</DataTemplate>
""");
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadEntriesAsync();
    }

    private void PackageSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyPackageSearchFilter();
    }

    private void EntrySearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyEntryFilter();
    }

    private void PackageRootsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplyEntryFilter();
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

    private async void EntriesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedEntry = _entriesListView.SelectedItem as AppPackageEntry;
        int sourceLoadVersion = ++_sourceLoadVersion;
        SetMetadata(_selectedEntry);

        if (_selectedEntry is null || _session is null)
        {
            SetSourceViewerText(string.Empty, useSyntaxHighlighting: false);
            return;
        }

        _inlineErrorInfoBar.IsOpen = false;
        SetSourceViewerText("Loading PeopleCode source...", useSyntaxHighlighting: false);

        AppPackageSourceResult result = await _browserService.GetSourceAsync(_session.Options, _selectedEntry);
        if (sourceLoadVersion != _sourceLoadVersion || !ReferenceEquals(_selectedEntry, _entriesListView.SelectedItem))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            SetSourceViewerText(string.Empty, useSyntaxHighlighting: false);
            _inlineErrorInfoBar.Message = result.ErrorMessage;
            _inlineErrorInfoBar.IsOpen = true;
            return;
        }

        SetSourceViewerText(
            string.IsNullOrWhiteSpace(result.SourceText)
            ? "No PeopleCode source text was returned for this entry."
            : result.SourceText,
            useSyntaxHighlighting: !string.IsNullOrWhiteSpace(result.SourceText));
    }

    private async Task LoadEntriesAsync()
    {
        if (_session is null)
        {
            return;
        }

        _inlineErrorInfoBar.IsOpen = false;
        SetSourceViewerText(string.Empty, useSyntaxHighlighting: false);
        _allPackageRoots.Clear();
        _filteredPackageRoots.Clear();
        _filteredEntries.Clear();
        _allEntries.Clear();
        _selectedEntry = null;
        _globalSearchVersion++;
        _sourceLoadVersion++;
        _packageSearchTextBox.Text = string.Empty;
        _entrySearchTextBox.Text = string.Empty;
        _globalSearchResults.Clear();
        _globalSourceSearchTextBox.Text = string.Empty;
        _globalSearchErrorInfoBar.IsOpen = false;
        _isGlobalSearchMode = false;
        _activeGlobalSearchText = string.Empty;
        UpdateGlobalSearchChrome();
        SetGlobalSearchStatus(string.Empty, false);
        SetMetadata(null);
        _metadataSummaryTextBlock.Text = "Loading App Package metadata...";

        AppPackageBrowseResult result = await _browserService.GetEntriesAsync(_session.Options);

        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            _metadataSummaryTextBlock.Text = "App Package metadata could not be loaded.";
            _inlineErrorInfoBar.Message = result.ErrorMessage;
            _inlineErrorInfoBar.IsOpen = true;
            return;
        }

        _allEntries.AddRange(result.Entries);

        foreach (string packageRoot in _allEntries
                     .Select(entry => entry.PackageRoot)
                     .Where(packageRoot => !string.IsNullOrWhiteSpace(packageRoot))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(packageRoot => packageRoot, StringComparer.OrdinalIgnoreCase))
        {
            _allPackageRoots.Add(packageRoot);
        }

        if (_allPackageRoots.Count > 0)
        {
            ApplyPackageSearchFilter();
            SelectVisiblePackage(null);
            ApplyEntryFilter();
            SelectVisibleEntry(null);
        }
        else
        {
            _metadataSummaryTextBlock.Text = "No App Package rows were returned from PSPCMTXT.";
        }
    }

    private void ApplyPackageSearchFilter()
    {
        string? previouslySelectedPackage = _packageRootsListView.SelectedItem as string;
        string searchText = _packageSearchTextBox.Text.Trim();

        _filteredPackageRoots.Clear();

        IEnumerable<string> matches = GetVisiblePackageRoots();
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            matches = matches.Where(packageRoot =>
                packageRoot.Contains(searchText, StringComparison.OrdinalIgnoreCase));
        }

        foreach (string packageRoot in matches)
        {
            _filteredPackageRoots.Add(packageRoot);
        }

        _noPackagesTextBlock.Text = _isGlobalSearchMode
            ? "No packages match the current global PeopleCode search."
            : "No packages found.";
        _noPackagesTextBlock.Visibility = _filteredPackageRoots.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        SelectVisiblePackage(previouslySelectedPackage);
        ApplyEntryFilter();
    }

    private void ApplyEntryFilter()
    {
        string? selectedPackage = _packageRootsListView.SelectedItem as string;
        AppPackageEntry? previouslySelectedEntry = _selectedEntry;
        string searchText = _entrySearchTextBox.Text.Trim();

        _filteredEntries.Clear();

        IEnumerable<AppPackageEntry> matches = GetVisibleEntries();
        if (!string.IsNullOrWhiteSpace(selectedPackage))
        {
            matches = matches.Where(entry =>
                entry.PackageRoot.Equals(selectedPackage, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            matches = [];
        }

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            matches = matches.Where(entry => EntryMatchesSearch(entry, searchText));
        }

        foreach (AppPackageEntry entry in matches)
        {
            _filteredEntries.Add(entry);
        }

        _noEntriesTextBlock.Text = _isGlobalSearchMode
            ? "No matching entries found for the selected package."
            : "No entries found.";
        _noEntriesTextBlock.Visibility =
            !string.IsNullOrWhiteSpace(selectedPackage) && _filteredEntries.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;

        SelectVisibleEntry(previouslySelectedEntry);

        if (_filteredEntries.Count == 0)
        {
            _selectedEntry = null;
            _sourceLoadVersion++;
            SetSourceViewerText(string.Empty, useSyntaxHighlighting: false);
            SetMetadata(null);
        }
    }

    private async Task SearchGlobalSourceAsync()
    {
        if (_session is null)
        {
            return;
        }

        string searchText = _globalSourceSearchTextBox.Text.Trim();
        int searchVersion = ++_globalSearchVersion;

        _globalSearchErrorInfoBar.IsOpen = false;
        _globalSearchResults.Clear();

        if (string.IsNullOrWhiteSpace(searchText))
        {
            SetGlobalSearchStatus("Enter source text to search.", true);
            return;
        }

        SetGlobalSearchStatus("Searching App Package PeopleCode...", true);
        _globalSourceSearchButton.IsEnabled = false;
        UpdateGlobalSearchChrome();

        AppPackageSourceSearchResult result = await _browserService.SearchSourceAsync(
            _session.Options,
            searchText,
            GlobalSearchResultLimit);

        if (searchVersion != _globalSearchVersion)
        {
            return;
        }

        _globalSourceSearchButton.IsEnabled = true;
        UpdateGlobalSearchChrome();

        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            SetGlobalSearchStatus(string.Empty, false);
            _globalSearchErrorInfoBar.Message = result.ErrorMessage;
            _globalSearchErrorInfoBar.IsOpen = true;
            return;
        }

        foreach (AppPackageSourceSearchMatch match in result.Matches)
        {
            _globalSearchResults.Add(match);
        }

        EnterGlobalSearchMode(searchText, result.WasLimited);
    }

    private void EnterGlobalSearchMode(string searchText, bool wasLimited)
    {
        _isGlobalSearchMode = true;
        _activeGlobalSearchText = searchText;
        _globalSearchErrorInfoBar.IsOpen = false;
        UpdateGlobalSearchChrome();
        RefreshSourceViewerFormatting();

        string? preferredPackage = _selectedEntry?.PackageRoot;
        AppPackageEntry? preferredEntry = _selectedEntry;

        ApplyPackageSearchFilter();
        SelectVisiblePackage(preferredPackage);
        ApplyEntryFilter();
        SelectVisibleEntry(preferredEntry);

        int packageCount = _globalSearchResults
            .Select(match => match.Entry.PackageRoot)
            .Where(packageRoot => !string.IsNullOrWhiteSpace(packageRoot))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        string countMessage = _globalSearchResults.Count == 0
            ? $"Global search mode is active for \"{searchText}\". No matching entries were found."
            : $"Global search mode is active for \"{searchText}\". {_globalSearchResults.Count} matching entries across {packageCount} package(s).";
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
        _globalSourceSearchTextBox.Text = string.Empty;
        _globalSearchErrorInfoBar.IsOpen = false;
        SetGlobalSearchStatus(string.Empty, false);
        UpdateGlobalSearchChrome();
        RefreshSourceViewerFormatting();

        string? preferredPackage = _selectedEntry?.PackageRoot;
        AppPackageEntry? preferredEntry = _selectedEntry;

        ApplyPackageSearchFilter();
        SelectVisiblePackage(preferredPackage);
        ApplyEntryFilter();
        SelectVisibleEntry(preferredEntry);
    }

    private IEnumerable<string> GetVisiblePackageRoots()
    {
        return _isGlobalSearchMode
            ? _globalSearchResults
                .Select(match => match.Entry.PackageRoot)
                .Where(packageRoot => !string.IsNullOrWhiteSpace(packageRoot))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(packageRoot => packageRoot, StringComparer.OrdinalIgnoreCase)
            : _allPackageRoots;
    }

    private IEnumerable<AppPackageEntry> GetVisibleEntries()
    {
        if (!_isGlobalSearchMode)
        {
            return _allEntries;
        }

        return _globalSearchResults
            .Select(match => FindExistingEntry(match.Entry))
            .Where(entry => entry is not null)
            .Cast<AppPackageEntry>()
            .Distinct(EntryIdentityComparer.Instance);
    }

    private AppPackageEntry? FindExistingEntry(AppPackageEntry searchEntry)
    {
        return _allEntries.FirstOrDefault(existingEntry => EntriesMatch(existingEntry, searchEntry));
    }

    private void UpdateGlobalSearchChrome()
    {
        bool hasActiveMode = _isGlobalSearchMode;
        _clearGlobalSearchButton.Visibility = hasActiveMode ? Visibility.Visible : Visibility.Collapsed;
        _clearGlobalSearchButton.IsEnabled = hasActiveMode;
        _globalSearchLimitationTextBlock.Visibility =
            hasActiveMode || !string.IsNullOrWhiteSpace(_activeGlobalSearchText)
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private void SelectVisiblePackage(string? preferredPackage)
    {
        string? nextPackage = _filteredPackageRoots.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(preferredPackage) &&
            _filteredPackageRoots.Contains(preferredPackage, StringComparer.OrdinalIgnoreCase))
        {
            nextPackage = preferredPackage;
        }

        if (!string.Equals(_packageRootsListView.SelectedItem as string, nextPackage, StringComparison.OrdinalIgnoreCase))
        {
            _packageRootsListView.SelectedItem = nextPackage;
        }
    }

    private void SelectVisibleEntry(AppPackageEntry? preferredEntry)
    {
        AppPackageEntry? nextEntry = _filteredEntries.FirstOrDefault();
        if (preferredEntry is not null)
        {
            AppPackageEntry? matchingEntry = _filteredEntries.FirstOrDefault(entry => EntriesMatch(entry, preferredEntry));
            if (matchingEntry is not null)
            {
                nextEntry = matchingEntry;
            }
        }

        if (!ReferenceEquals(_entriesListView.SelectedItem, nextEntry))
        {
            _entriesListView.SelectedItem = nextEntry;
        }
    }

    private static bool EntryMatchesSearch(AppPackageEntry entry, string searchText)
    {
        return entry.DisplayName.Contains(searchText, StringComparison.OrdinalIgnoreCase)
            || entry.EntryType.Contains(searchText, StringComparison.OrdinalIgnoreCase)
            || entry.PackageRoot.Contains(searchText, StringComparison.OrdinalIgnoreCase)
            || entry.ObjectValue2.Contains(searchText, StringComparison.OrdinalIgnoreCase)
            || entry.ObjectValue3.Contains(searchText, StringComparison.OrdinalIgnoreCase)
            || entry.ObjectValue4.Contains(searchText, StringComparison.OrdinalIgnoreCase)
            || entry.ObjectValue5.Contains(searchText, StringComparison.OrdinalIgnoreCase)
            || entry.ObjectValue6.Contains(searchText, StringComparison.OrdinalIgnoreCase)
            || entry.ObjectValue7.Contains(searchText, StringComparison.OrdinalIgnoreCase);
    }

    private static bool EntriesMatch(AppPackageEntry left, AppPackageEntry right)
    {
        return left.PackageRoot.Equals(right.PackageRoot, StringComparison.OrdinalIgnoreCase)
            && left.ObjectValue2.Equals(right.ObjectValue2, StringComparison.OrdinalIgnoreCase)
            && left.ObjectValue3.Equals(right.ObjectValue3, StringComparison.OrdinalIgnoreCase)
            && left.ObjectValue4.Equals(right.ObjectValue4, StringComparison.OrdinalIgnoreCase)
            && left.ObjectValue5.Equals(right.ObjectValue5, StringComparison.OrdinalIgnoreCase)
            && left.ObjectValue6.Equals(right.ObjectValue6, StringComparison.OrdinalIgnoreCase)
            && left.ObjectValue7.Equals(right.ObjectValue7, StringComparison.OrdinalIgnoreCase);
    }

    private void SetGlobalSearchStatus(string text, bool isVisible)
    {
        _globalSearchStatusTextBlock.Text = text;
        _globalSearchStatusTextBlock.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SetSourceViewerText(string text, bool useSyntaxHighlighting)
    {
        _currentSourceText = text;
        _currentSourceUsesSyntaxHighlighting = useSyntaxHighlighting;
        _currentSourceMatchRanges = _isGlobalSearchMode && !string.IsNullOrWhiteSpace(_activeGlobalSearchText)
            ? PeopleCodeSourceFormatter.GetMatchRanges(text, _activeGlobalSearchText)
            : Array.Empty<TextRange>();
        _activeSourceMatchIndex = _currentSourceMatchRanges.Count > 0 ? 0 : -1;
        PeopleCodeSourceFormatter.ApplyFormatting(
            _sourceRichTextBlock,
            text,
            useSyntaxHighlighting,
            _isGlobalSearchMode ? _activeGlobalSearchText : null,
            _activeSourceMatchIndex);
        UpdateSourceMatchChrome();

        if (_activeSourceMatchIndex >= 0)
        {
            ScrollActiveMatchIntoView();
        }
        else
        {
            _sourceScrollViewer.ChangeView(0, 0, null, true);
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
            _sourceRichTextBlock,
            _currentSourceText,
            _currentSourceUsesSyntaxHighlighting,
            _isGlobalSearchMode ? _activeGlobalSearchText : null,
            _activeSourceMatchIndex);
        UpdateSourceMatchChrome();
    }

    private void UpdateSourceMatchChrome()
    {
        bool hasNavigableMatches = _isGlobalSearchMode && _currentSourceMatchRanges.Count > 0;
        _previousSourceMatchButton.IsEnabled = hasNavigableMatches;
        _nextSourceMatchButton.IsEnabled = hasNavigableMatches;

        _sourceMatchStatusTextBlock.Text = hasNavigableMatches
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

        double scrollableHeight = Math.Max(0d, _sourceScrollViewer.ExtentHeight - _sourceScrollViewer.ViewportHeight);
        double scrollableWidth = Math.Max(0d, _sourceScrollViewer.ExtentWidth - _sourceScrollViewer.ViewportWidth);

        double targetLineRatio = totalLineCount <= 1
            ? 0d
            : Math.Clamp((double)precedingLineBreaks / (totalLineCount - 1), 0d, 1d);
        double targetColumnRatio = maxLineLength <= 1
            ? 0d
            : Math.Clamp((double)columnIndex / maxLineLength, 0d, 1d);

        double verticalPadding = Math.Max(24d, _sourceScrollViewer.ViewportHeight * 0.2d);
        double horizontalPadding = Math.Max(24d, _sourceScrollViewer.ViewportWidth * 0.1d);
        double verticalOffset = Math.Max(0d, (scrollableHeight * targetLineRatio) - verticalPadding);
        double horizontalOffset = Math.Max(0d, (scrollableWidth * targetColumnRatio) - horizontalPadding);

        _sourceScrollViewer.ChangeView(horizontalOffset, verticalOffset, null, false);
    }

    private void SetMetadata(AppPackageEntry? entry)
    {
        if (entry is null || string.IsNullOrWhiteSpace(entry.PackageRoot))
        {
            _selectedEntryTitleTextBlock.Text = string.Empty;
            _selectedEntryTypeTextBlock.Text = string.Empty;
            _metadataSummaryTextBlock.Text = "Select an App Package entry to view available identifiers.";
            return;
        }

        _selectedEntryTitleTextBlock.Text = entry.DisplayName;
        _selectedEntryTypeTextBlock.Text = entry.EntryType;
        _metadataSummaryTextBlock.Text =
            $"OBJECTVALUE2={ValueOrPlaceholder(entry.ObjectValue2)}, OBJECTVALUE3={ValueOrPlaceholder(entry.ObjectValue3)}, OBJECTVALUE4={ValueOrPlaceholder(entry.ObjectValue4)}, OBJECTVALUE5={ValueOrPlaceholder(entry.ObjectValue5)}, OBJECTVALUE6={ValueOrPlaceholder(entry.ObjectValue6)}, OBJECTVALUE7={ValueOrPlaceholder(entry.ObjectValue7)}, LASTUPDOPRID={ValueOrPlaceholder(entry.LastUpdatedBy)}, LASTUPDDTTM={entry.LastUpdatedDateTime?.ToString("u") ?? "(blank)"}";
    }

    private static string ValueOrPlaceholder(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "(blank)" : value;
    }

    private sealed class EntryIdentityComparer : IEqualityComparer<AppPackageEntry>
    {
        public static EntryIdentityComparer Instance { get; } = new();

        public bool Equals(AppPackageEntry? x, AppPackageEntry? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null || y is null)
            {
                return false;
            }

            return EntriesMatch(x, y);
        }

        public int GetHashCode(AppPackageEntry obj)
        {
            return HashCode.Combine(
                obj.PackageRoot.ToUpperInvariant(),
                obj.ObjectValue2.ToUpperInvariant(),
                obj.ObjectValue3.ToUpperInvariant(),
                obj.ObjectValue4.ToUpperInvariant(),
                obj.ObjectValue5.ToUpperInvariant(),
                obj.ObjectValue6.ToUpperInvariant(),
                obj.ObjectValue7.ToUpperInvariant());
        }
    }
}
