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

public sealed class AllObjectsPeopleCodeBrowserView : UserControl
{
    private const int MaxResultsPerType = 200;
    private const string EmptyStateMessage = "Enter a search term to search across all supported PeopleCode object types.";

    private readonly AllObjectsPeopleCodeBrowserService _browserService = new();
    private readonly DetachedSourceWindowManager _detachedSourceWindowManager;
    private readonly List<AllObjectsSearchItem> _allResults = [];
    private readonly ObservableCollection<AllObjectsSearchGroup> _visibleGroups = [];
    private readonly ObservableCollection<AllObjectsSearchItem> _visibleResults = [];

    private readonly TextBox _searchTextBox;
    private readonly Button _searchButton;
    private readonly Button _clearButton;
    private readonly InfoBar _searchErrorInfoBar;
    private readonly TextBlock _searchStatusTextBlock;
    private readonly TextBlock _searchEmptyStateTextBlock;
    private readonly ListView _groupsListView;
    private readonly TextBlock _groupsEmptyStateTextBlock;
    private readonly ListView _resultsListView;
    private readonly TextBlock _resultsEmptyStateTextBlock;
    private readonly TextBlock _selectedItemTitleTextBlock;
    private readonly TextBlock _selectedItemSubtitleTextBlock;
    private readonly TextBlock _metadataSummaryTextBlock;
    private readonly Button _openDetachedSourceButton;
    private readonly Button _previousSourceMatchButton;
    private readonly Button _nextSourceMatchButton;
    private readonly TextBlock _sourceMatchStatusTextBlock;
    private readonly RichTextBlock _sourceRichTextBlock;
    private readonly ScrollViewer _sourceScrollViewer;

    private OracleConnectionSession? _session;
    private AllObjectsSearchItem? _selectedItem;
    private string _activeSearchText = string.Empty;
    private int _searchVersion;
    private int _sourceLoadVersion;
    private string _currentSourceText = string.Empty;
    private bool _hasLoadedSelectedSource;
    private IReadOnlyList<TextRange> _currentSourceMatchRanges = Array.Empty<TextRange>();
    private int _activeSourceMatchIndex = -1;

    public AllObjectsPeopleCodeBrowserView(DetachedSourceWindowManager detachedSourceWindowManager)
    {
        _detachedSourceWindowManager = detachedSourceWindowManager;
        _searchTextBox = new TextBox
        {
            PlaceholderText = "Search PeopleCode across App Package, App Engine, Record, Page, and Component"
        };
        _searchTextBox.KeyDown += SearchTextBox_KeyDown;
        _searchTextBox.TextChanged += SearchTextBox_TextChanged;

        _searchButton = new Button
        {
            Content = "Search",
            IsEnabled = false
        };
        _searchButton.Click += SearchButton_Click;

        _clearButton = new Button
        {
            Content = "Clear",
            IsEnabled = false
        };
        _clearButton.Click += ClearButton_Click;

        _searchErrorInfoBar = new InfoBar
        {
            IsClosable = false,
            IsOpen = false,
            Severity = InfoBarSeverity.Warning
        };

        _searchStatusTextBlock = BuildSecondaryTextBlock();
        _searchEmptyStateTextBlock = BuildSecondaryTextBlock();

        _groupsListView = new ListView
        {
            ItemsSource = _visibleGroups,
            SelectionMode = ListViewSelectionMode.Single,
            ItemTemplate = BuildGroupTemplate()
        };
        ScrollViewer.SetVerticalScrollBarVisibility(_groupsListView, ScrollBarVisibility.Auto);
        ScrollViewer.SetHorizontalScrollBarVisibility(_groupsListView, ScrollBarVisibility.Disabled);
        _groupsListView.SelectionChanged += GroupsListView_SelectionChanged;

        _groupsEmptyStateTextBlock = BuildSecondaryTextBlock();

        _resultsListView = new ListView
        {
            ItemsSource = _visibleResults,
            SelectionMode = ListViewSelectionMode.Single,
            ItemTemplate = BuildResultTemplate()
        };
        ScrollViewer.SetVerticalScrollBarVisibility(_resultsListView, ScrollBarVisibility.Auto);
        ScrollViewer.SetHorizontalScrollBarVisibility(_resultsListView, ScrollBarVisibility.Disabled);
        _resultsListView.SelectionChanged += ResultsListView_SelectionChanged;

        _resultsEmptyStateTextBlock = BuildSecondaryTextBlock();

        _selectedItemTitleTextBlock = new TextBlock
        {
            TextWrapping = TextWrapping.WrapWholeWords
        };
        _selectedItemTitleTextBlock.Style = Application.Current.Resources["BodyStrongTextBlockStyle"] as Style;

        _selectedItemSubtitleTextBlock = new TextBlock
        {
            TextWrapping = TextWrapping.WrapWholeWords
        };

        _metadataSummaryTextBlock = new TextBlock
        {
            TextWrapping = TextWrapping.WrapWholeWords
        };

        _openDetachedSourceButton = new Button
        {
            Content = "Open",
            IsEnabled = false
        };
        ToolTipService.SetToolTip(_openDetachedSourceButton, "Open in new window");
        _openDetachedSourceButton.Click += OpenDetachedSourceButton_Click;

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

        _sourceMatchStatusTextBlock = BuildSecondaryTextBlock();
        _sourceRichTextBlock = new RichTextBlock
        {
            FontFamily = new FontFamily("Consolas"),
            IsTextSelectionEnabled = true,
            MinHeight = 300,
            TextWrapping = TextWrapping.NoWrap
        };
        _sourceRichTextBlock.Blocks.Add(new Paragraph());

        _sourceScrollViewer = new ScrollViewer
        {
            Content = _sourceRichTextBlock,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        Content = BuildLayout();
        ResetViewState();
    }

    public void SetSession(OracleConnectionSession session)
    {
        _session = session;
        ResetViewState();
        UpdateSearchChrome();
    }

    private UIElement BuildLayout()
    {
        Grid root = new()
        {
            RowSpacing = 12,
            ColumnSpacing = 16
        };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        Border searchBorder = BuildPlainBorder(BuildSearchPanel());
        root.Children.Add(searchBorder);

        Grid.SetRow(_searchErrorInfoBar, 1);
        root.Children.Add(_searchErrorInfoBar);

        Grid contentGrid = new() { ColumnSpacing = 16 };
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(340) });
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(contentGrid, 2);
        root.Children.Add(contentGrid);

        contentGrid.Children.Add(BuildSectionBorder("Object Types", _groupsListView, _groupsEmptyStateTextBlock));
        Border resultsBorder = BuildSectionBorder("Matches", _resultsListView, _resultsEmptyStateTextBlock);
        Grid.SetColumn(resultsBorder, 1);
        contentGrid.Children.Add(resultsBorder);

        Grid detailGrid = new() { RowSpacing = 16 };
        detailGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        detailGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(detailGrid, 2);
        contentGrid.Children.Add(detailGrid);

        StackPanel metadataPanel = new() { Spacing = 6 };
        TextBlock metadataTitle = new() { Text = "Metadata" };
        metadataTitle.Style = Application.Current.Resources["SubtitleTextBlockStyle"] as Style;
        metadataPanel.Children.Add(metadataTitle);
        metadataPanel.Children.Add(_selectedItemTitleTextBlock);
        metadataPanel.Children.Add(_selectedItemSubtitleTextBlock);
        metadataPanel.Children.Add(_metadataSummaryTextBlock);
        detailGrid.Children.Add(BuildPlainBorder(metadataPanel));

        Grid sourceGrid = new() { RowSpacing = 8 };
        sourceGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        sourceGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        Grid sourceHeaderGrid = new() { ColumnSpacing = 8 };
        sourceHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        sourceHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        sourceHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        sourceHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        sourceHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        TextBlock sourceTitle = new() { Text = "PeopleCode Source" };
        sourceTitle.Style = Application.Current.Resources["SubtitleTextBlockStyle"] as Style;
        sourceHeaderGrid.Children.Add(sourceTitle);
        Grid.SetColumn(_openDetachedSourceButton, 1);
        sourceHeaderGrid.Children.Add(_openDetachedSourceButton);
        Grid.SetColumn(_sourceMatchStatusTextBlock, 2);
        sourceHeaderGrid.Children.Add(_sourceMatchStatusTextBlock);
        Grid.SetColumn(_previousSourceMatchButton, 3);
        sourceHeaderGrid.Children.Add(_previousSourceMatchButton);
        Grid.SetColumn(_nextSourceMatchButton, 4);
        sourceHeaderGrid.Children.Add(_nextSourceMatchButton);
        sourceGrid.Children.Add(sourceHeaderGrid);

        Grid.SetRow(_sourceScrollViewer, 1);
        sourceGrid.Children.Add(_sourceScrollViewer);

        Border sourceBorder = BuildPlainBorder(sourceGrid);
        Grid.SetRow(sourceBorder, 1);
        detailGrid.Children.Add(sourceBorder);

        return root;
    }

    private UIElement BuildSearchPanel()
    {
        Grid grid = new()
        {
            RowSpacing = 8,
            ColumnSpacing = 8
        };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        TextBlock title = new() { Text = "All Objects Search" };
        title.Style = Application.Current.Resources["SubtitleTextBlockStyle"] as Style;
        Grid.SetColumnSpan(title, 3);
        grid.Children.Add(title);

        Grid.SetRow(_searchTextBox, 1);
        grid.Children.Add(_searchTextBox);

        Grid.SetRow(_searchButton, 1);
        Grid.SetColumn(_searchButton, 1);
        grid.Children.Add(_searchButton);

        Grid.SetRow(_clearButton, 1);
        Grid.SetColumn(_clearButton, 2);
        grid.Children.Add(_clearButton);

        Grid.SetRow(_searchStatusTextBlock, 2);
        Grid.SetColumnSpan(_searchStatusTextBlock, 3);
        grid.Children.Add(_searchStatusTextBlock);

        Grid.SetRow(_searchEmptyStateTextBlock, 2);
        Grid.SetColumnSpan(_searchEmptyStateTextBlock, 3);
        grid.Children.Add(_searchEmptyStateTextBlock);

        return grid;
    }

    private static Border BuildSectionBorder(string title, FrameworkElement content, TextBlock emptyStateTextBlock)
    {
        Grid sectionGrid = new() { RowSpacing = 8 };
        sectionGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        sectionGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        sectionGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        TextBlock titleBlock = new() { Text = title };
        titleBlock.Style = Application.Current.Resources["SubtitleTextBlockStyle"] as Style;
        sectionGrid.Children.Add(titleBlock);

        Grid.SetRow(content, 1);
        sectionGrid.Children.Add(content);

        Grid.SetRow(emptyStateTextBlock, 2);
        sectionGrid.Children.Add(emptyStateTextBlock);

        Border border = BuildPlainBorder(sectionGrid);
        border.MinHeight = 420;
        return border;
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

    private static TextBlock BuildSecondaryTextBlock()
    {
        return new TextBlock
        {
            Foreground = Application.Current.Resources["TextFillColorSecondaryBrush"] as Brush,
            TextWrapping = TextWrapping.WrapWholeWords
        };
    }

    private static DataTemplate BuildGroupTemplate()
    {
        return (DataTemplate)XamlReader.Load("""
<DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
    <TextBlock Padding="8" Text="{Binding DisplayLabel}" TextWrapping="WrapWholeWords" />
</DataTemplate>
""");
    }

    private static DataTemplate BuildResultTemplate()
    {
        return (DataTemplate)XamlReader.Load("""
<DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
    <StackPanel Padding="8" Spacing="4">
        <TextBlock Text="{Binding PrimaryText}" Style="{ThemeResource BodyStrongTextBlockStyle}" TextWrapping="WrapWholeWords" />
        <TextBlock Text="{Binding SecondaryText}" Foreground="{ThemeResource TextFillColorSecondaryBrush}" TextWrapping="WrapWholeWords" />
        <TextBlock Text="{Binding MatchPreview}" Foreground="{ThemeResource TextFillColorSecondaryBrush}" TextWrapping="WrapWholeWords" MaxLines="3" />
    </StackPanel>
</DataTemplate>
""");
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateSearchChrome();
    }

    private async void SearchButton_Click(object sender, RoutedEventArgs e)
    {
        await SearchAsync();
    }

    private async void SearchTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter)
        {
            return;
        }

        e.Handled = true;
        await SearchAsync();
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        ResetViewState(clearSearchText: true);
    }

    private void GroupsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RefreshVisibleResults();
    }

    private async void ResultsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedItem = _resultsListView.SelectedItem as AllObjectsSearchItem;
        int sourceLoadVersion = ++_sourceLoadVersion;
        _hasLoadedSelectedSource = false;
        UpdateDetachedSourceChrome();
        UpdateMetadata(_selectedItem);

        if (_selectedItem is null || _session is null)
        {
            SetSourceViewerText(string.Empty);
            return;
        }

        SetSourceViewerText("Loading PeopleCode source...");
        AllObjectsSourceResult result = await _browserService.GetSourceAsync(_session.Options, _selectedItem);
        if (sourceLoadVersion != _sourceLoadVersion || !ReferenceEquals(_selectedItem, _resultsListView.SelectedItem))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            SetSourceViewerText(string.Empty);
            _searchErrorInfoBar.Message = $"{_selectedItem.ObjectType}: {result.ErrorMessage}";
            _searchErrorInfoBar.IsOpen = true;
            return;
        }

        _hasLoadedSelectedSource = true;
        SetSourceViewerText(
            string.IsNullOrWhiteSpace(result.SourceText)
                ? "No PeopleCode source text was returned for the selected result."
                : result.SourceText);
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

    private async Task SearchAsync()
    {
        if (_session is null)
        {
            return;
        }

        string searchText = _searchTextBox.Text.Trim();
        int searchVersion = ++_searchVersion;

        _searchErrorInfoBar.IsOpen = false;

        if (string.IsNullOrWhiteSpace(searchText))
        {
            ResetViewState(clearSearchText: false);
            return;
        }

        _activeSearchText = searchText;
        _allResults.Clear();
        _visibleGroups.Clear();
        _visibleResults.Clear();
        _selectedItem = null;
        _resultsListView.SelectedItem = null;
        _groupsListView.SelectedItem = null;
        SetSourceViewerText(string.Empty);
        UpdateMetadata(null);
        SetSearchStatus("Searching all supported PeopleCode object types...");
        _searchEmptyStateTextBlock.Visibility = Visibility.Collapsed;
        _searchButton.IsEnabled = false;
        UpdatePaneEmptyStates();

        AllObjectsSearchResult result = await _browserService.SearchAsync(_session.Options, searchText, MaxResultsPerType);
        if (searchVersion != _searchVersion)
        {
            return;
        }

        foreach (AllObjectsSearchItem item in result.Items)
        {
            _allResults.Add(item);
        }

        foreach (AllObjectsSearchGroup group in result.Groups)
        {
            _visibleGroups.Add(group);
        }

        if (result.FailureMessages.Count > 0)
        {
            _searchErrorInfoBar.Message = "Partial search failure: " + string.Join(" | ", result.FailureMessages);
            _searchErrorInfoBar.IsOpen = true;
        }

        string statusMessage = _allResults.Count == 0
            ? $"No matching PeopleCode objects were found for \"{searchText}\"."
            : $"Found {_allResults.Count} matching result(s) across {_visibleGroups.Count} object type group(s) for \"{searchText}\".";
        if (result.WasLimited)
        {
            statusMessage += $" Showing up to {MaxResultsPerType} result(s) per object type.";
        }

        SetSearchStatus(statusMessage);
        _searchButton.IsEnabled = true;
        UpdateSearchChrome();

        if (_visibleGroups.Count > 0)
        {
            _groupsListView.SelectedItem = _visibleGroups[0];
        }
        else
        {
            RefreshVisibleResults();
        }
    }

    private void RefreshVisibleResults()
    {
        AllObjectsSearchGroup? selectedGroup = _groupsListView.SelectedItem as AllObjectsSearchGroup;
        AllObjectsSearchItem? preferredItem = _selectedItem;

        _visibleResults.Clear();

        IEnumerable<AllObjectsSearchItem> items = selectedGroup is null
            ? []
            : _allResults.Where(item => item.ObjectType.Equals(selectedGroup.ObjectType, StringComparison.OrdinalIgnoreCase));

        foreach (AllObjectsSearchItem item in items)
        {
            _visibleResults.Add(item);
        }

        AllObjectsSearchItem? nextItem = _visibleResults.FirstOrDefault();
        if (preferredItem is not null)
        {
            AllObjectsSearchItem? matchingItem = _visibleResults.FirstOrDefault(item =>
                item.ObjectType.Equals(preferredItem.ObjectType, StringComparison.OrdinalIgnoreCase) &&
                item.MetadataTitle.Equals(preferredItem.MetadataTitle, StringComparison.OrdinalIgnoreCase) &&
                item.MetadataSubtitle.Equals(preferredItem.MetadataSubtitle, StringComparison.OrdinalIgnoreCase));
            if (matchingItem is not null)
            {
                nextItem = matchingItem;
            }
        }

        if (!ReferenceEquals(_resultsListView.SelectedItem, nextItem))
        {
            _resultsListView.SelectedItem = nextItem;
        }

        if (_visibleResults.Count == 0)
        {
            _selectedItem = null;
            _sourceLoadVersion++;
            _hasLoadedSelectedSource = false;
            UpdateMetadata(null);
            SetSourceViewerText(string.Empty);
        }

        UpdatePaneEmptyStates();
    }

    private void ResetViewState(bool clearSearchText = true)
    {
        _searchVersion++;
        _sourceLoadVersion++;
        _allResults.Clear();
        _visibleGroups.Clear();
        _visibleResults.Clear();
        _selectedItem = null;
        _activeSearchText = string.Empty;
        _hasLoadedSelectedSource = false;
        _groupsListView.SelectedItem = null;
        _resultsListView.SelectedItem = null;
        if (clearSearchText)
        {
            _searchTextBox.Text = string.Empty;
        }

        _searchErrorInfoBar.IsOpen = false;
        SetSearchStatus(string.Empty);
        _searchEmptyStateTextBlock.Text = _session is null
            ? "Connect to Oracle to search across all supported PeopleCode object types."
            : EmptyStateMessage;
        _searchEmptyStateTextBlock.Visibility = Visibility.Visible;
        UpdateMetadata(null);
        SetSourceViewerText(string.Empty);
        UpdateSearchChrome();
        UpdatePaneEmptyStates();
    }

    private void UpdateSearchChrome()
    {
        bool hasSession = _session is not null;
        bool hasSearchText = !string.IsNullOrWhiteSpace(_searchTextBox.Text);
        _searchButton.IsEnabled = hasSession && hasSearchText;
        _clearButton.IsEnabled = hasSession && (!string.IsNullOrWhiteSpace(_activeSearchText) || hasSearchText);
    }

    private void SetSearchStatus(string text)
    {
        _searchStatusTextBlock.Text = text;
        _searchStatusTextBlock.Visibility = string.IsNullOrWhiteSpace(text) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void UpdatePaneEmptyStates()
    {
        bool hasActiveSearch = !string.IsNullOrWhiteSpace(_activeSearchText);

        _groupsEmptyStateTextBlock.Text = hasActiveSearch
            ? "No object type groups have matches for the current search."
            : "Run a search to see matching object type groups.";
        _groupsEmptyStateTextBlock.Visibility = _visibleGroups.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        _resultsEmptyStateTextBlock.Text = hasActiveSearch
            ? "Select an object type group to see matching results."
            : "Search across all objects to populate this pane.";
        _resultsEmptyStateTextBlock.Visibility = _visibleResults.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateMetadata(AllObjectsSearchItem? item)
    {
        if (item is null)
        {
            _selectedItemTitleTextBlock.Text = string.Empty;
            _selectedItemSubtitleTextBlock.Text = string.Empty;
            _metadataSummaryTextBlock.Text = string.IsNullOrWhiteSpace(_activeSearchText)
                ? EmptyStateMessage
                : "Select a matching result to view metadata and PeopleCode source.";
            return;
        }

        _selectedItemTitleTextBlock.Text = item.MetadataTitle;
        _selectedItemSubtitleTextBlock.Text = $"{item.ObjectType} | {item.MetadataSubtitle}";
        _metadataSummaryTextBlock.Text = item.MetadataSummary;
    }

    private bool CanOpenDetachedSource()
    {
        return _selectedItem is not null && _hasLoadedSelectedSource;
    }

    private void UpdateDetachedSourceChrome()
    {
        _openDetachedSourceButton.IsEnabled = CanOpenDetachedSource();
    }

    private DetachedPeopleCodeSourceContext BuildDetachedSourceContext()
    {
        return DetachedPeopleCodeSourceContextFactory.Create(
            _session,
            _selectedItem?.ObjectType ?? "PeopleCode",
            _selectedItemTitleTextBlock.Text,
            _selectedItemSubtitleTextBlock.Text,
            _metadataSummaryTextBlock.Text,
            string.Empty,
            _currentSourceText,
            _activeSearchText,
            useSyntaxHighlighting: true);
    }

    private void SetSourceViewerText(string text)
    {
        _currentSourceText = text ?? string.Empty;
        _currentSourceMatchRanges = !string.IsNullOrWhiteSpace(_activeSearchText)
            ? PeopleCodeSourceFormatter.GetMatchRanges(_currentSourceText, _activeSearchText)
            : Array.Empty<TextRange>();
        _activeSourceMatchIndex = _currentSourceMatchRanges.Count > 0 ? 0 : -1;

        PeopleCodeSourceFormatter.ApplyFormatting(
            _sourceRichTextBlock,
            _currentSourceText,
            useSyntaxHighlighting: true,
            string.IsNullOrWhiteSpace(_activeSearchText) ? null : _activeSearchText,
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

        UpdateDetachedSourceChrome();
    }

    private void RefreshSourceViewerFormatting()
    {
        _currentSourceMatchRanges = !string.IsNullOrWhiteSpace(_activeSearchText)
            ? PeopleCodeSourceFormatter.GetMatchRanges(_currentSourceText, _activeSearchText)
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
            useSyntaxHighlighting: true,
            string.IsNullOrWhiteSpace(_activeSearchText) ? null : _activeSearchText,
            _activeSourceMatchIndex);

        UpdateSourceMatchChrome();
    }

    private void UpdateSourceMatchChrome()
    {
        bool hasNavigableMatches = _currentSourceMatchRanges.Count > 0;
        _previousSourceMatchButton.IsEnabled = hasNavigableMatches;
        _nextSourceMatchButton.IsEnabled = hasNavigableMatches;
        _sourceMatchStatusTextBlock.Text = hasNavigableMatches
            ? $"Match {_activeSourceMatchIndex + 1} of {_currentSourceMatchRanges.Count}"
            : !string.IsNullOrWhiteSpace(_activeSearchText)
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
}
