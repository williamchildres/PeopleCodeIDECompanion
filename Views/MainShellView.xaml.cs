using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PeopleCodeIDECompanion.Models;
using PeopleCodeIDECompanion.Services;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;

namespace PeopleCodeIDECompanion.Views;

public sealed partial class MainShellView : UserControl
{
    private readonly PeopleCodeInterfaceView _peopleCodeInterfaceView = new();
    private readonly OracleConnectionView _oracleConnectionView = new();
    private readonly ReferenceExplorerView _referenceExplorerView = new();
    private readonly long _isPaneOpenCallbackToken;

    public MainShellView()
    {
        InitializeComponent();

        _oracleConnectionView.BrowserRequested += OracleConnectionView_BrowserRequested;
        BuildObjectStatusPanel();
        _isPaneOpenCallbackToken = AppNavigationView.RegisterPropertyChangedCallback(
            NavigationView.IsPaneOpenProperty,
            (_, _) => UpdatePaneFooterLayout());
        Unloaded += MainShellView_Unloaded;
        UpdatePaneFooterLayout();

        ContentHost.Content = _oracleConnectionView;
        AppNavigationView.SelectedItem = AppNavigationView.MenuItems[0];
    }

    public IReadOnlyList<PeopleCodeObjectStatusItem> ObjectStatuses => _peopleCodeInterfaceView.ObjectStatuses;

    private void OracleConnectionView_BrowserRequested(object? sender, OracleConnectionSession session)
    {
        _peopleCodeInterfaceView.SetSession(session);
        _peopleCodeInterfaceView.ShowAppPackage();
        if (App.MainWindow is MainWindow mainWindow)
        {
            mainWindow.UpdateConnectionTitle(session);
        }
        ContentHost.Content = _peopleCodeInterfaceView;
        AppNavigationView.SelectedItem = PeopleCodeInterfaceNavigationItem;
    }

    private void AppNavigationView_SelectionChanged(
        NavigationView sender,
        NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer?.Tag is not string destination)
        {
            return;
        }

        NavigateTo(destination);
    }

    private async void ObjectStatusRefreshButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string objectType })
        {
            return;
        }

        await _peopleCodeInterfaceView.RefreshObjectTypeAsync(objectType);
    }

    private void BuildObjectStatusPanel()
    {
        ObjectStatusPanel.Children.Clear();
        CompactObjectStatusPanel.Children.Clear();

        foreach (PeopleCodeObjectStatusItem status in ObjectStatuses)
        {
            StatusRowControls row = CreateStatusRow(status);
            UpdateStatusRow(row, status);
            Button compactButton = CreateCompactStatusButton(status);
            UpdateCompactStatusButton(compactButton, status);
            status.PropertyChanged += StatusItem_PropertyChanged;
            ObjectStatusPanel.Children.Add(row.Root);
            CompactObjectStatusPanel.Children.Add(compactButton);
        }

        if (_peopleCodeInterfaceView.ObjectStatuses is INotifyCollectionChanged collectionChanged)
        {
            collectionChanged.CollectionChanged += ObjectStatuses_CollectionChanged;
        }
    }

    private void ObjectStatuses_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        BuildObjectStatusPanel();
    }

    private void StatusItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not PeopleCodeObjectStatusItem status)
        {
            return;
        }

        foreach (UIElement child in ObjectStatusPanel.Children)
        {
            if (child is Grid row && ReferenceEquals(row.Tag, status))
            {
                UpdateStatusRow(new StatusRowControls(
                    row,
                    (TextBlock)((StackPanel)row.Children[0]).Children[0],
                    (TextBlock)((StackPanel)row.Children[0]).Children[1],
                    (TextBlock)((StackPanel)row.Children[0]).Children[2],
                    (Button)row.Children[1]),
                    status);
                break;
            }
        }

        foreach (UIElement child in CompactObjectStatusPanel.Children)
        {
            if (child is Button button && ReferenceEquals(button.Tag, status.ObjectTypeName))
            {
                UpdateCompactStatusButton(button, status);
                break;
            }
        }
    }

    private StatusRowControls CreateStatusRow(PeopleCodeObjectStatusItem status)
    {
        Grid row = new()
        {
            Margin = new Thickness(0, 0, 0, 6),
            ColumnSpacing = 8,
            Tag = status
        };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        TextBlock nameTextBlock = new()
        {
            Style = Application.Current.Resources["CaptionTextBlockStyle"] as Style
        };
        TextBlock statusTextBlock = new()
        {
            Foreground = Application.Current.Resources["TextFillColorPrimaryBrush"] as Brush
        };
        TextBlock lastLoadedTextBlock = new()
        {
            Style = Application.Current.Resources["CaptionTextBlockStyle"] as Style,
            Foreground = Application.Current.Resources["TextFillColorSecondaryBrush"] as Brush
        };

        StackPanel textPanel = new() { Spacing = 1 };
        textPanel.Children.Add(nameTextBlock);
        textPanel.Children.Add(statusTextBlock);
        textPanel.Children.Add(lastLoadedTextBlock);
        row.Children.Add(textPanel);

        Button refreshButton = new()
        {
            MinWidth = 30,
            MinHeight = 30,
            Padding = new Thickness(6),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Tag = status.ObjectTypeName,
            Content = new FontIcon
            {
                Glyph = "\uE72C",
                FontSize = 12
            }
        };
        refreshButton.Click += ObjectStatusRefreshButton_Click;
        Grid.SetColumn(refreshButton, 1);
        row.Children.Add(refreshButton);

        return new StatusRowControls(row, nameTextBlock, statusTextBlock, lastLoadedTextBlock, refreshButton);
    }

    private static void UpdateStatusRow(StatusRowControls row, PeopleCodeObjectStatusItem status)
    {
        row.NameTextBlock.Text = status.ObjectTypeName;
        row.StatusTextBlock.Text = status.StatusText;
        row.LastLoadedTextBlock.Text = status.LastLoadedDisplayText;
        row.RefreshButton.IsEnabled = status.CanRefresh;
    }

    private Button CreateCompactStatusButton(PeopleCodeObjectStatusItem status)
    {
        Button button = new()
        {
            Width = 32,
            Height = 32,
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Center,
            Tag = status.ObjectTypeName,
            Content = new FontIcon
            {
                Glyph = GetCompactGlyph(status.ObjectTypeName),
                FontSize = 14
            }
        };
        button.Click += ObjectStatusRefreshButton_Click;
        return button;
    }

    private void UpdatePaneFooterLayout()
    {
        bool showExpandedFooter = AppNavigationView.IsPaneOpen;
        ExpandedObjectStatusBorder.Visibility = showExpandedFooter ? Visibility.Visible : Visibility.Collapsed;
        CompactObjectStatusBorder.Visibility = showExpandedFooter ? Visibility.Collapsed : Visibility.Visible;

        foreach (PeopleCodeObjectStatusItem status in ObjectStatuses)
        {
            foreach (UIElement child in CompactObjectStatusPanel.Children)
            {
                if (child is Button button && ReferenceEquals(button.Tag, status.ObjectTypeName))
                {
                    UpdateCompactStatusButton(button, status);
                    break;
                }
            }
        }
    }

    private void NavigateTo(string destination)
    {
        ContentHost.Content = destination switch
        {
            "OracleConnection" => _oracleConnectionView,
            "PeopleCodeInterface" => _peopleCodeInterfaceView,
            "ReferenceExplorer" => _referenceExplorerView,
            _ => _referenceExplorerView
        };
    }

    private static void UpdateCompactStatusButton(Button button, PeopleCodeObjectStatusItem status)
    {
        button.IsEnabled = status.CanRefresh;
        ToolTipService.SetToolTip(
            button,
            $"{status.ObjectTypeName}\nStatus: {status.StatusText}\n{status.LastLoadedDisplayText}");
    }

    private static string GetCompactGlyph(string objectTypeName) => objectTypeName switch
    {
        AllObjectsPeopleCodeBrowserService.AppPackageMode => "\uE8B7",
        AllObjectsPeopleCodeBrowserService.AppEngineMode => "\uE768",
        AllObjectsPeopleCodeBrowserService.RecordMode => "\uE8D2",
        AllObjectsPeopleCodeBrowserService.PageMode => "\uE7C3",
        AllObjectsPeopleCodeBrowserService.ComponentMode => "\uE71D",
        _ => "\uE72C"
    };

    private sealed record StatusRowControls(
        Grid Root,
        TextBlock NameTextBlock,
        TextBlock StatusTextBlock,
        TextBlock LastLoadedTextBlock,
        Button RefreshButton);

    private void MainShellView_Unloaded(object sender, RoutedEventArgs e)
    {
        AppNavigationView.UnregisterPropertyChangedCallback(
            NavigationView.IsPaneOpenProperty,
            _isPaneOpenCallbackToken);
        Unloaded -= MainShellView_Unloaded;
    }
}
