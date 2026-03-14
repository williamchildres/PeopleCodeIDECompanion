using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using PeopleCodeIDECompanion.Models;
using PeopleCodeIDECompanion.Services;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Windows.UI.Core;

namespace PeopleCodeIDECompanion.Views;

public sealed partial class MainShellView : UserControl
{
    private static readonly Uri GitHubRepositoryUri = new("https://github.com/williamchildres/PeopleCodeIDECompanion");
    private readonly OracleSessionManager _sessionManager = new();
    private readonly SavedOracleConnectionStore _savedConnectionStore = new();
    private readonly SecureCredentialStore _secureCredentialStore = new();
    private readonly OracleConnectionTester _connectionTester = new();
    private readonly PeopleCodeInterfaceView _peopleCodeInterfaceView;
    private readonly PeopleCodeOverviewView _peopleCodeOverviewView;
    private readonly OracleConnectionView _oracleConnectionView;
    private readonly ReferenceExplorerView _referenceExplorerView = new();
    private readonly long _isPaneOpenCallbackToken;
    private INotifyCollectionChanged? _currentStatusCollection;
    private readonly List<PeopleCodeObjectStatusItem> _trackedStatuses = [];

    public MainShellView()
    {
        InitializeComponent();

        _peopleCodeInterfaceView = new PeopleCodeInterfaceView(_sessionManager);
        _peopleCodeOverviewView = new PeopleCodeOverviewView(_sessionManager);
        _oracleConnectionView = new OracleConnectionView();
        _oracleConnectionView.BrowserRequested += OracleConnectionView_BrowserRequested;
        _oracleConnectionView.ProfileSaved += OracleConnectionView_ProfileSaved;
        _oracleConnectionView.ProfileDeleted += OracleConnectionView_ProfileDeleted;
        _peopleCodeInterfaceView.ActiveWorkspaceChanged += PeopleCodeInterfaceView_ActiveWorkspaceChanged;
        _peopleCodeOverviewView.NavigateToPeopleCodeObjectRequested += PeopleCodeOverviewView_NavigateToPeopleCodeObjectRequested;
        BuildObjectStatusPanel();

        _isPaneOpenCallbackToken = AppNavigationView.RegisterPropertyChangedCallback(
            NavigationView.IsPaneOpenProperty,
            (_, _) => UpdatePaneFooterLayout());
        KeyDown += MainShellView_KeyDown;
        Loaded += MainShellView_Loaded;
        Unloaded += MainShellView_Unloaded;
        UpdatePaneFooterLayout();

        ContentHost.Content = _peopleCodeInterfaceView;
        AppNavigationView.SelectedItem = PeopleCodeInterfaceNavigationItem;
    }

    public IReadOnlyList<PeopleCodeObjectStatusItem> ObjectStatuses => _peopleCodeInterfaceView.ObjectStatuses;

    public FrameworkElement TitleBarDragRegion => TitleBarDragRegionElement;

    private async void MainShellView_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= MainShellView_Loaded;
        await AutoLoginAsync();
    }

    private async void OracleConnectionView_BrowserRequested(object? sender, OracleConnectionSession session)
    {
        _sessionManager.AddOrUpdate(session);
        _peopleCodeInterfaceView.TrackSession(session);
        await UpdateLastConnectedAsync(session);
        _peopleCodeInterfaceView.ClearWarning();
        ShellInfoBar.IsOpen = false;
        NavigateToPeopleCodeInterface();
    }

    private void OracleConnectionView_ProfileSaved(object? sender, ProfileSavedEventArgs args)
    {
        OracleConnectionSession? promotedSession = null;
        if (!string.IsNullOrWhiteSpace(args.PreviousSessionProfileId))
        {
            promotedSession = _sessionManager.PromoteSession(args.PreviousSessionProfileId, args.Profile);
        }

        OracleConnectionSession? trackedSession = promotedSession;
        if (trackedSession is null && args.SavedSession is not null)
        {
            _sessionManager.AddOrUpdate(args.SavedSession, selectSession: false);
            trackedSession = args.SavedSession;
        }

        if (trackedSession is not null)
        {
            _peopleCodeInterfaceView.TrackSession(trackedSession);
            if (App.MainWindow is MainWindow mainWindow)
            {
                mainWindow.UpdateConnectionTitle(_sessionManager.SelectedSession, _sessionManager.Sessions.Count);
            }
        }
    }

    private void OracleConnectionView_ProfileDeleted(object? sender, ProfileDeletedEventArgs args)
    {
        if (_sessionManager.RemoveSession(args.ProfileId))
        {
            _peopleCodeInterfaceView.RefreshSessions();
            if (App.MainWindow is MainWindow mainWindow)
            {
                mainWindow.UpdateConnectionTitle(_sessionManager.SelectedSession, _sessionManager.Sessions.Count);
            }
        }
    }

    private void PeopleCodeInterfaceView_ActiveWorkspaceChanged(object? sender, EventArgs e)
    {
        RebindStatusSubscriptions();
        if (App.MainWindow is MainWindow mainWindow)
        {
            mainWindow.UpdateConnectionTitle(_sessionManager.SelectedSession, _sessionManager.Sessions.Count);
        }
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

    private void PaneToggleButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleSidebar();
    }

    private async void FileSettingsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        await ShowSettingsDialogAsync();
    }

    private async void FileAboutMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ContentDialog dialog = new()
        {
            Title = "About PeopleCodeIDECompanion",
            CloseButtonText = "Close",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock
                    {
                        Text = "PeopleCodeIDECompanion",
                        Style = Application.Current.Resources["TitleTextBlockStyle"] as Style
                    },
                    new TextBlock
                    {
                        Text = "Read-only PeopleCode browsing, search, overview, and cross-profile compare for Oracle-backed PeopleSoft environments.",
                        TextWrapping = TextWrapping.WrapWholeWords
                    },
                    new TextBlock
                    {
                        Text = GitHubRepositoryUri.ToString(),
                        Foreground = Application.Current.Resources["TextFillColorSecondaryBrush"] as Brush,
                        TextWrapping = TextWrapping.WrapWholeWords
                    }
                }
            }
        };

        await dialog.ShowAsync();
    }

    private void EditCopyMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ExecuteTextEditCommand(EditCommand.Copy);
    }

    private void EditCutMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ExecuteTextEditCommand(EditCommand.Cut);
    }

    private void EditPasteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ExecuteTextEditCommand(EditCommand.Paste);
    }

    private void EditDeleteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ExecuteTextEditCommand(EditCommand.Delete);
    }

    private void ViewToggleSidebarMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ToggleSidebar();
    }

    private void ViewToggleFindMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ToggleFind();
    }

    private void WindowMinimizeMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (App.MainWindow is MainWindow mainWindow)
        {
            mainWindow.MinimizeWindow();
        }
    }

    private async void HelpGitHubMenuItem_Click(object sender, RoutedEventArgs e)
    {
        await Launcher.LaunchUriAsync(GitHubRepositoryUri);
    }

    private async void ObjectStatusRefreshButton_Click(object sender, RoutedEventArgs e)
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
            ObjectStatusPanel.Children.Add(row.Root);
            CompactObjectStatusPanel.Children.Add(compactButton);
        }
    }

    private void RebindStatusSubscriptions()
    {
        if (_currentStatusCollection is not null)
        {
            _currentStatusCollection.CollectionChanged -= ObjectStatuses_CollectionChanged;
        }

        foreach (PeopleCodeObjectStatusItem status in _trackedStatuses)
        {
            status.PropertyChanged -= StatusItem_PropertyChanged;
        }

        _trackedStatuses.Clear();
        _currentStatusCollection = ObjectStatuses as INotifyCollectionChanged;
        if (_currentStatusCollection is not null)
        {
            _currentStatusCollection.CollectionChanged += ObjectStatuses_CollectionChanged;
        }

        foreach (PeopleCodeObjectStatusItem status in ObjectStatuses)
        {
            status.PropertyChanged += StatusItem_PropertyChanged;
            _trackedStatuses.Add(status);
        }

        BuildObjectStatusPanel();
        UpdatePaneFooterLayout();
    }

    private void ObjectStatuses_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebindStatusSubscriptions();
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
            "PeopleCodeInterface" => _peopleCodeInterfaceView,
            "PeopleCodeOverview" => _peopleCodeOverviewView,
            "ReferenceExplorer" => _referenceExplorerView,
            _ => _peopleCodeInterfaceView
        };
    }

    private void NavigateToPeopleCodeInterface()
    {
        NavigateTo("PeopleCodeInterface");
        AppNavigationView.SelectedItem = PeopleCodeInterfaceNavigationItem;
    }

    private async void PeopleCodeOverviewView_NavigateToPeopleCodeObjectRequested(
        object? sender,
        PeopleCodeObjectNavigationRequest e)
    {
        bool opened = await _peopleCodeInterfaceView.OpenItemAsync(e.ProfileId, e.ObjectType, e.SourceKey);
        if (opened)
        {
            NavigateToPeopleCodeInterface();
        }
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

    private async System.Threading.Tasks.Task AutoLoginAsync()
    {
        IReadOnlyList<SavedOracleConnectionProfile> profiles = await _savedConnectionStore.LoadAsync();
        List<string> failures = [];
        int successCount = 0;

        foreach (SavedOracleConnectionProfile profile in profiles.Where(savedProfile => savedProfile.AutoLoginEnabled))
        {
            string? password = await _secureCredentialStore.LoadPasswordAsync(profile.CredentialTargetId);
            if (string.IsNullOrWhiteSpace(password))
            {
                failures.Add($"{profile.DisplayName}: no stored password was available.");
                continue;
            }

            OracleConnectionOptions options = new()
            {
                Host = profile.Host,
                Port = profile.Port,
                ServiceName = profile.ServiceName,
                Username = profile.Username,
                Password = password
            };

            OracleConnectionTestResult result = await _connectionTester.TestConnectionAsync(options);
            if (!result.IsSuccess)
            {
                failures.Add($"{profile.DisplayName}: {result.Details}");
                continue;
            }

            OracleConnectionSession session = new()
            {
                ProfileId = profile.ProfileId,
                DisplayName = profile.DisplayName,
                CredentialTargetId = profile.CredentialTargetId,
                Options = options
            };

            _sessionManager.AddOrUpdate(session, selectSession: successCount == 0);
            await UpdateLastConnectedAsync(session);
            successCount++;
        }

        if (successCount > 0)
        {
            _peopleCodeInterfaceView.RefreshSessions();
            NavigateToPeopleCodeInterface();
        }

        if (failures.Count > 0)
        {
            string failureMessage = "Auto-login could not connect: " + string.Join(" | ", failures);
            ShellInfoBar.Message = failureMessage;
            ShellInfoBar.Severity = successCount > 0 ? InfoBarSeverity.Warning : InfoBarSeverity.Error;
            ShellInfoBar.IsOpen = true;
            _peopleCodeInterfaceView.ShowWarning(failureMessage);
        }

        if (App.MainWindow is MainWindow mainWindow)
        {
            mainWindow.UpdateConnectionTitle(_sessionManager.SelectedSession, _sessionManager.Sessions.Count);
        }

        RebindStatusSubscriptions();
    }

    private async System.Threading.Tasks.Task UpdateLastConnectedAsync(OracleConnectionSession session)
    {
        if (string.IsNullOrWhiteSpace(session.ProfileId))
        {
            return;
        }

        await _savedConnectionStore.UpdateLastConnectedAsync(session.ProfileId, DateTimeOffset.Now);
    }

    private sealed record StatusRowControls(
        Grid Root,
        TextBlock NameTextBlock,
        TextBlock StatusTextBlock,
        TextBlock LastLoadedTextBlock,
        Button RefreshButton);

    private async System.Threading.Tasks.Task ShowSettingsDialogAsync()
    {
        ContentDialog dialog = new()
        {
            Title = "Settings",
            CloseButtonText = "Close",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
            Content = _oracleConnectionView
        };

        void CloseSettingsOnBrowserRequest(object? sender, OracleConnectionSession session)
        {
            dialog.Hide();
        }

        _oracleConnectionView.BrowserRequested += CloseSettingsOnBrowserRequest;
        try
        {
            await dialog.ShowAsync();
        }
        finally
        {
            _oracleConnectionView.BrowserRequested -= CloseSettingsOnBrowserRequest;
        }
    }

    private void ToggleSidebar()
    {
        AppNavigationView.IsPaneOpen = !AppNavigationView.IsPaneOpen;
    }

    private void ToggleFind()
    {
        NavigateToPeopleCodeInterface();
        _peopleCodeInterfaceView.FocusGlobalSearch();
    }

    private void MainShellView_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (!IsControlPressed())
        {
            return;
        }

        if (e.Key == VirtualKey.B)
        {
            e.Handled = true;
            ToggleSidebar();
            return;
        }

        if (e.Key == VirtualKey.F && !ReferenceEquals(ContentHost.Content, _peopleCodeInterfaceView))
        {
            e.Handled = true;
            ToggleFind();
        }
    }

    private void ExecuteTextEditCommand(EditCommand command)
    {
        DependencyObject? focusedElement = FocusManager.GetFocusedElement(XamlRoot) as DependencyObject;
        switch (focusedElement)
        {
            case TextBox textBox:
                ExecuteTextBoxCommand(textBox, command);
                break;
            case PasswordBox passwordBox when command is EditCommand.Paste or EditCommand.Delete:
                ExecutePasswordBoxCommand(passwordBox, command);
                break;
        }
    }

    private static void ExecuteTextBoxCommand(TextBox textBox, EditCommand command)
    {
        switch (command)
        {
            case EditCommand.Copy:
                textBox.CopySelectionToClipboard();
                break;
            case EditCommand.Cut:
                textBox.CutSelectionToClipboard();
                break;
            case EditCommand.Paste:
                textBox.PasteFromClipboard();
                break;
            case EditCommand.Delete:
                if (textBox.SelectionLength > 0)
                {
                    int start = textBox.SelectionStart;
                    textBox.Text = textBox.Text.Remove(start, textBox.SelectionLength);
                    textBox.SelectionStart = start;
                }
                else if (textBox.SelectionStart < textBox.Text.Length)
                {
                    int start = textBox.SelectionStart;
                    textBox.Text = textBox.Text.Remove(start, 1);
                    textBox.SelectionStart = start;
                }
                break;
        }
    }

    private static void ExecutePasswordBoxCommand(PasswordBox passwordBox, EditCommand command)
    {
        if (command != EditCommand.Delete)
        {
            return;
        }

        passwordBox.Password = string.Empty;
    }

    private static bool IsControlPressed()
    {
        CoreVirtualKeyStates controlState = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);
        return (controlState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
    }

    private void MainShellView_Unloaded(object sender, RoutedEventArgs e)
    {
        AppNavigationView.UnregisterPropertyChangedCallback(
            NavigationView.IsPaneOpenProperty,
            _isPaneOpenCallbackToken);

        if (_currentStatusCollection is not null)
        {
            _currentStatusCollection.CollectionChanged -= ObjectStatuses_CollectionChanged;
        }

        foreach (PeopleCodeObjectStatusItem status in _trackedStatuses)
        {
            status.PropertyChanged -= StatusItem_PropertyChanged;
        }

        _oracleConnectionView.BrowserRequested -= OracleConnectionView_BrowserRequested;
        _oracleConnectionView.ProfileSaved -= OracleConnectionView_ProfileSaved;
        _oracleConnectionView.ProfileDeleted -= OracleConnectionView_ProfileDeleted;
        _peopleCodeInterfaceView.ActiveWorkspaceChanged -= PeopleCodeInterfaceView_ActiveWorkspaceChanged;
        _peopleCodeOverviewView.NavigateToPeopleCodeObjectRequested -= PeopleCodeOverviewView_NavigateToPeopleCodeObjectRequested;
        Unloaded -= MainShellView_Unloaded;
    }

    private enum EditCommand
    {
        Copy,
        Cut,
        Paste,
        Delete
    }
}
