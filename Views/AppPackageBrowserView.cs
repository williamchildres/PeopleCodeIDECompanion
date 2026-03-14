using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using PeopleCodeIDECompanion.Models;
using PeopleCodeIDECompanion.Services;

namespace PeopleCodeIDECompanion.Views;

public sealed class AppPackageBrowserView : UserControl
{
    private readonly AppPackageBrowserService _browserService = new();
    private readonly List<AppPackageEntry> _allEntries = [];
    private readonly ObservableCollection<AppPackageEntry> _filteredEntries = [];
    private readonly ObservableCollection<string> _packageRoots = [];

    private readonly TextBlock _connectionSummaryTextBlock;
    private readonly Button _refreshButton;
    private readonly InfoBar _inlineErrorInfoBar;
    private readonly ListView _packageRootsListView;
    private readonly ListView _entriesListView;
    private readonly TextBlock _selectedEntryTitleTextBlock;
    private readonly TextBlock _selectedEntryTypeTextBlock;
    private readonly TextBlock _metadataSummaryTextBlock;
    private readonly TextBox _sourceTextBox;

    private OracleConnectionSession? _session;
    private AppPackageEntry? _selectedEntry;

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

        _packageRootsListView = new ListView
        {
            ItemsSource = _packageRoots
        };
        _packageRootsListView.SelectionChanged += PackageRootsListView_SelectionChanged;

        _entriesListView = new ListView
        {
            ItemsSource = _filteredEntries,
            ItemTemplate = BuildEntryTemplate()
        };
        _entriesListView.SelectionChanged += EntriesListView_SelectionChanged;

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

        _sourceTextBox = new TextBox
        {
            AcceptsReturn = true,
            FontFamily = new FontFamily("Consolas"),
            IsReadOnly = true,
            MinHeight = 300,
            TextWrapping = TextWrapping.NoWrap
        };

        Content = BuildLayout();
        SetMetadata(null);
    }

    public void SetSession(OracleConnectionSession session)
    {
        _session = session;
        _connectionSummaryTextBlock.Text = string.IsNullOrWhiteSpace(session.DisplayName)
            ? $"Connected as {session.Options.Username} to {session.Options.Host}:{session.Options.Port}/{session.Options.ServiceName}"
            : $"Connected with profile {session.DisplayName} as {session.Options.Username} to {session.Options.Host}:{session.Options.Port}/{session.Options.ServiceName}";
        _refreshButton.IsEnabled = true;
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

        contentGrid.Children.Add(BuildSectionBorder("Packages", _packageRootsListView));
        Border entriesHost = BuildSectionBorder("Entries", _entriesListView);
        Grid.SetColumn(entriesHost, 1);
        contentGrid.Children.Add(entriesHost);

        Grid detailGrid = new() { RowSpacing = 16 };
        detailGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        detailGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(detailGrid, 2);
        contentGrid.Children.Add(detailGrid);

        StackPanel metadataPanel = new() { Spacing = 6 };
        TextBlock metadataTitle = new() { Text = "Metadata" };
        metadataTitle.Style = Application.Current.Resources["SubtitleTextBlockStyle"] as Style;
        metadataPanel.Children.Add(metadataTitle);
        metadataPanel.Children.Add(_selectedEntryTitleTextBlock);
        metadataPanel.Children.Add(_selectedEntryTypeTextBlock);
        metadataPanel.Children.Add(_metadataSummaryTextBlock);
        detailGrid.Children.Add(BuildPlainBorder(metadataPanel));

        Grid sourceGrid = new() { RowSpacing = 8 };
        sourceGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        sourceGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        TextBlock sourceTitle = new() { Text = "PeopleCode Source" };
        sourceTitle.Style = Application.Current.Resources["SubtitleTextBlockStyle"] as Style;
        sourceGrid.Children.Add(sourceTitle);
        Grid.SetRow(_sourceTextBox, 1);
        ScrollViewer sourceScrollViewer = new()
        {
            Content = _sourceTextBox,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        Grid.SetRow(sourceScrollViewer, 1);
        sourceGrid.Children.Add(sourceScrollViewer);
        Border sourceBorder = BuildPlainBorder(sourceGrid);
        Grid.SetRow(sourceBorder, 1);
        detailGrid.Children.Add(sourceBorder);

        return root;
    }

    private static Border BuildSectionBorder(string title, ListView listView)
    {
        StackPanel stack = new() { Spacing = 8 };
        TextBlock titleBlock = new() { Text = title };
        titleBlock.Style = Application.Current.Resources["SubtitleTextBlockStyle"] as Style;
        stack.Children.Add(titleBlock);
        stack.Children.Add(listView);
        return BuildPlainBorder(stack);
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

    private void PackageRootsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplyPackageFilter(_packageRootsListView.SelectedItem as string);
    }

    private async void EntriesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedEntry = _entriesListView.SelectedItem as AppPackageEntry;
        SetMetadata(_selectedEntry);

        if (_selectedEntry is null || _session is null)
        {
            _sourceTextBox.Text = string.Empty;
            return;
        }

        _inlineErrorInfoBar.IsOpen = false;
        _sourceTextBox.Text = "Loading PeopleCode source...";

        AppPackageSourceResult result = await _browserService.GetSourceAsync(_session.Options, _selectedEntry);

        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            _sourceTextBox.Text = string.Empty;
            _inlineErrorInfoBar.Message = result.ErrorMessage;
            _inlineErrorInfoBar.IsOpen = true;
            return;
        }

        _sourceTextBox.Text = string.IsNullOrWhiteSpace(result.SourceText)
            ? "No PeopleCode source text was returned for this entry."
            : result.SourceText;
    }

    private async Task LoadEntriesAsync()
    {
        if (_session is null)
        {
            return;
        }

        _inlineErrorInfoBar.IsOpen = false;
        _sourceTextBox.Text = string.Empty;
        _packageRoots.Clear();
        _filteredEntries.Clear();
        _allEntries.Clear();
        _selectedEntry = null;
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
            _packageRoots.Add(packageRoot);
        }

        if (_packageRoots.Count > 0)
        {
            _packageRootsListView.SelectedItem = _packageRoots[0];
            ApplyPackageFilter(_packageRoots[0]);
        }
        else
        {
            _metadataSummaryTextBlock.Text = "No App Package rows were returned from PSPCMTXT.";
        }
    }

    private void ApplyPackageFilter(string? packageRoot)
    {
        _filteredEntries.Clear();

        IEnumerable<AppPackageEntry> matches = _allEntries;
        if (!string.IsNullOrWhiteSpace(packageRoot))
        {
            matches = matches.Where(entry =>
                entry.PackageRoot.Equals(packageRoot, StringComparison.OrdinalIgnoreCase));
        }

        foreach (AppPackageEntry entry in matches)
        {
            _filteredEntries.Add(entry);
        }

        _entriesListView.SelectedItem = _filteredEntries.FirstOrDefault();
        _sourceTextBox.Text = string.Empty;
        SetMetadata(_entriesListView.SelectedItem as AppPackageEntry);
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
}
