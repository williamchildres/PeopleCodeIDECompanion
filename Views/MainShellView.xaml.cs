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
        KeyDown += MainShellView_KeyDown;
        Loaded += MainShellView_Loaded;
        Unloaded += MainShellView_Unloaded;
        UpdateShellContentLayout();

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
    }

    private void ObjectStatuses_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebindStatusSubscriptions();
    }

    private void StatusItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not PeopleCodeObjectStatusItem)
        {
            return;
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
        UpdateShellContentLayout();
    }

    private void ShellContentScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateShellContentLayout();
    }

    private void UpdateShellContentLayout()
    {
        if (ContentHost.Content is not FrameworkElement content)
        {
            return;
        }

        double viewportWidth = Math.Max(0d, ShellContentScrollViewer.ActualWidth);
        double viewportHeight = Math.Max(0d, ShellContentScrollViewer.ActualHeight);
        double targetWidth = Math.Max(viewportWidth, content.MinWidth);
        double targetHeight = Math.Max(viewportHeight, content.MinHeight);

        ShellContentViewport.Width = targetWidth;
        ShellContentViewport.Height = targetHeight;
        ContentHost.Width = targetWidth;
        ContentHost.Height = targetHeight;
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
