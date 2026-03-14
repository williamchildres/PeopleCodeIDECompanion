using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using PeopleCodeIDECompanion.Models;
using PeopleCodeIDECompanion.Services;
using Windows.System;
using Windows.UI.Core;

namespace PeopleCodeIDECompanion.Views;

public sealed partial class PeopleCodeInterfaceView : UserControl
{
    private const string AllObjectsMode = AllObjectsPeopleCodeBrowserService.AllObjectsMode;
    private const string AppPackageMode = AllObjectsPeopleCodeBrowserService.AppPackageMode;
    private const string AppEngineMode = AllObjectsPeopleCodeBrowserService.AppEngineMode;
    private const string RecordMode = AllObjectsPeopleCodeBrowserService.RecordMode;
    private const string PageMode = AllObjectsPeopleCodeBrowserService.PageMode;
    private const string ComponentMode = AllObjectsPeopleCodeBrowserService.ComponentMode;

    private readonly OracleSessionManager _sessionManager;
    private readonly DetachedSourceWindowManager _detachedSourceWindowManager = new();
    private readonly PeopleCodeCompareWindowManager _compareWindowManager;
    private readonly Dictionary<string, ProfileWorkspace> _workspaces = [];
    private INotifyCollectionChanged? _trackedStatusCollection;
    private readonly List<PeopleCodeObjectStatusItem> _trackedStatusItems = [];
    private bool _isUpdatingModeSelection;
    private bool _isUpdatingProfileSelection;

    public PeopleCodeInterfaceView(OracleSessionManager sessionManager)
    {
        _sessionManager = sessionManager;
        _compareWindowManager = new PeopleCodeCompareWindowManager(sessionManager);
        InitializeComponent();
        AddHandler(KeyDownEvent, new KeyEventHandler(PeopleCodeInterfaceView_KeyDown), true);
        ProfileComboBox.ItemsSource = ConnectedProfiles;
        _sessionManager.SessionsChanged += SessionManager_SessionsChanged;
        _sessionManager.SelectedSessionChanged += SessionManager_SelectedSessionChanged;
        SyncWorkspaces();
        UpdateHeader();
        ShowCurrentWorkspace();
    }

    public event EventHandler? ActiveWorkspaceChanged;

    public ObservableCollection<OracleConnectionSession> ConnectedProfiles { get; } = [];

    public IReadOnlyList<PeopleCodeObjectStatusItem> ObjectStatuses =>
        ActiveWorkspace is null
            ? Array.Empty<PeopleCodeObjectStatusItem>()
            : ActiveWorkspace.StatusStore.Items;

    public void ShowWarning(string message)
    {
        HeaderInfoBar.Severity = InfoBarSeverity.Warning;
        HeaderInfoBar.Message = message;
        HeaderInfoBar.IsOpen = !string.IsNullOrWhiteSpace(message);
    }

    public void ClearWarning()
    {
        HeaderInfoBar.IsOpen = false;
    }

    public Task RefreshObjectTypeAsync(string objectType)
    {
        return ActiveWorkspace?.RefreshAsync(objectType) ?? Task.CompletedTask;
    }

    public void TrackSession(OracleConnectionSession session)
    {
        ConnectedProfiles.UpsertSession(session);

        if (_workspaces.TryGetValue(session.ProfileId, out ProfileWorkspace? workspace))
        {
            workspace.SetSession(session);
        }
        else
        {
            _workspaces[session.ProfileId] = new ProfileWorkspace(session, _detachedSourceWindowManager, _compareWindowManager);
        }

        SyncProfileSelection(_sessionManager.SelectedSession ?? session);
        UpdateHeader();
        ShowCurrentWorkspace();
    }

    public void RefreshSessions()
    {
        SyncWorkspaces();
    }

    public void ShowAppPackage() => ShowMode(AppPackageMode);

    public void ShowAllObjects() => ShowMode(AllObjectsMode);

    public void ShowAppEngine() => ShowMode(AppEngineMode);

    public void ShowRecord() => ShowMode(RecordMode);

    public void ShowPage() => ShowMode(PageMode);

    public void ShowComponent() => ShowMode(ComponentMode);

    public async Task<bool> OpenItemAsync(string profileId, string objectType, object? sourceKey)
    {
        if (sourceKey is null)
        {
            return false;
        }

        _sessionManager.SelectSession(profileId);
        ProfileWorkspace? workspace = ActiveWorkspace;
        if (workspace is null)
        {
            return false;
        }

        string mode = objectType switch
        {
            AllObjectsPeopleCodeBrowserService.AppPackageMode => AppPackageMode,
            AllObjectsPeopleCodeBrowserService.AppEngineMode => AppEngineMode,
            AllObjectsPeopleCodeBrowserService.RecordMode => RecordMode,
            AllObjectsPeopleCodeBrowserService.PageMode => PageMode,
            AllObjectsPeopleCodeBrowserService.ComponentMode => ComponentMode,
            _ => AllObjectsMode
        };

        workspace.CurrentMode = mode;
        SetSelectedMode(mode);
        ModeSummaryTextBlock.Text = GetModeSummary(mode);
        ModeContentHost.Content = workspace.GetContentForMode(mode);
        UpdateModeContentLayout();
        return await workspace.OpenItemAsync(mode, sourceKey);
    }

    public void FocusGlobalSearch()
    {
        ActiveWorkspace?.FocusGlobalSearch();
    }

    private ProfileWorkspace? ActiveWorkspace =>
        _sessionManager.SelectedSession is null
            ? null
            : _workspaces.GetValueOrDefault(_sessionManager.SelectedSession.ProfileId);

    private void SessionManager_SessionsChanged(object? sender, EventArgs e)
    {
        SyncWorkspaces();
    }

    private void SessionManager_SelectedSessionChanged(object? sender, OracleConnectionSession? session)
    {
        SyncProfileSelection(session);
        ShowCurrentWorkspace();
    }

    private async void RefreshModeButton_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveWorkspace is null)
        {
            return;
        }

        await RefreshObjectTypeAsync(ActiveWorkspace.CurrentMode);
        UpdateHeader();
        UpdateWorkspaceStatusSummary();
    }

    private void PeopleCodeInterfaceView_KeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key != VirtualKey.F || !IsControlPressed())
        {
            return;
        }

        args.Handled = true;
        FocusGlobalSearch();
    }

    private void SyncWorkspaces()
    {
        HashSet<string> activeProfileIds = _sessionManager.Sessions
            .Select(session => session.ProfileId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (OracleConnectionSession session in _sessionManager.Sessions)
        {
            ConnectedProfiles.UpsertSession(session);

            if (_workspaces.TryGetValue(session.ProfileId, out ProfileWorkspace? workspace))
            {
                workspace.SetSession(session);
            }
            else
            {
                _workspaces[session.ProfileId] = new ProfileWorkspace(session, _detachedSourceWindowManager, _compareWindowManager);
            }
        }

        foreach (OracleConnectionSession removedSession in ConnectedProfiles
                     .Where(session => !activeProfileIds.Contains(session.ProfileId))
                     .ToList())
        {
            ConnectedProfiles.Remove(removedSession);
        }

        foreach (string profileId in _workspaces.Keys
                     .Where(profileId => !activeProfileIds.Contains(profileId))
                     .ToList())
        {
            _workspaces.Remove(profileId);
        }

        SyncProfileSelection(_sessionManager.SelectedSession);
        UpdateHeader();
        ShowCurrentWorkspace();
    }

    private void SyncProfileSelection(OracleConnectionSession? session)
    {
        if (_isUpdatingProfileSelection)
        {
            return;
        }

        _isUpdatingProfileSelection = true;
        ProfileComboBox.SelectedItem = session is null
            ? null
            : ConnectedProfiles.FirstOrDefault(connectedProfile =>
                connectedProfile.ProfileId.Equals(session.ProfileId, StringComparison.OrdinalIgnoreCase));
        _isUpdatingProfileSelection = false;
    }

    private void ShowCurrentWorkspace()
    {
        ProfileWorkspace? workspace = ActiveWorkspace;
        if (workspace is null)
        {
            RebindWorkspaceStatusSubscriptions();
            ModeContentHost.Content = null;
            ModeSummaryTextBlock.Text = "Connect to at least one Oracle profile to browse read-only PeopleCode objects.";
            ConnectedProfilesSummaryTextBlock.Text = ConnectedProfiles.Count == 0
                ? "No active database sessions."
                : $"{ConnectedProfiles.Count} active database session(s).";
            ConnectionSummaryTextBlock.Text = "No database selected";
            LastRefreshSummaryTextBlock.Text = "Last refresh: not loaded";
            UpdateWorkspaceStatusSummary();
            ActiveWorkspaceChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        RebindWorkspaceStatusSubscriptions();
        SetSelectedMode(workspace.CurrentMode);
        ModeSummaryTextBlock.Text = GetModeSummary(workspace.CurrentMode);
        ConnectedProfilesSummaryTextBlock.Text = ConnectedProfiles.Count == 1
            ? "1 active database session."
            : $"{ConnectedProfiles.Count} active database sessions.";
        ActiveProfileSummaryTextBlock.Text = BuildActiveProfileSummary(workspace.Session);
        ConnectionSummaryTextBlock.Text = BuildConnectionSummary(workspace.Session);
        LastRefreshSummaryTextBlock.Text = BuildLastRefreshSummary(workspace.StatusStore.Items);
        ModeContentHost.Content = workspace.GetContentForMode(workspace.CurrentMode);
        UpdateModeContentLayout();
        UpdateWorkspaceStatusSummary();
        ActiveWorkspaceChanged?.Invoke(this, EventArgs.Empty);
    }

    private static string BuildActiveProfileSummary(OracleConnectionSession session)
    {
        return session.DisplayName;
    }

    private static string BuildConnectionSummary(OracleConnectionSession session)
    {
        return $"{session.Options.Host}:{session.Options.Port}/{session.Options.ServiceName}";
    }

    private void ProfileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingProfileSelection || ProfileComboBox.SelectedItem is not OracleConnectionSession session)
        {
            return;
        }

        _sessionManager.SelectSession(session.ProfileId);
    }

    private void ObjectTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingModeSelection)
        {
            return;
        }

        switch (ObjectTypeComboBox.SelectedItem as string)
        {
            case AllObjectsMode:
                ShowAllObjects();
                return;
            case AppEngineMode:
                ShowAppEngine();
                return;
            case RecordMode:
                ShowRecord();
                return;
            case PageMode:
                ShowPage();
                return;
            case ComponentMode:
                ShowComponent();
                return;
        }

        ShowAppPackage();
    }

    private void ShowMode(string mode)
    {
        ProfileWorkspace? workspace = ActiveWorkspace;
        if (workspace is null)
        {
            return;
        }

        workspace.CurrentMode = mode;
        SetSelectedMode(mode);
        ModeSummaryTextBlock.Text = GetModeSummary(mode);
        ModeContentHost.Content = workspace.GetContentForMode(mode);
        UpdateModeContentLayout();
    }

    private void ModeContentScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateModeContentLayout();
    }

    private void UpdateModeContentLayout()
    {
        if (ModeContentHost.Content is not FrameworkElement content)
        {
            return;
        }

        double viewportWidth = Math.Max(0d, ModeContentScrollViewer.ActualWidth);
        double viewportHeight = Math.Max(0d, ModeContentScrollViewer.ActualHeight);
        double targetWidth = Math.Max(viewportWidth, content.MinWidth);
        double targetHeight = Math.Max(viewportHeight, content.MinHeight);
        ModeContentViewport.Width = targetWidth;
        ModeContentViewport.Height = targetHeight;
        ModeContentHost.Width = targetWidth;
        ModeContentHost.Height = targetHeight;
        content.Width = targetWidth;
        content.Height = targetHeight;
        content.HorizontalAlignment = HorizontalAlignment.Stretch;
        content.VerticalAlignment = VerticalAlignment.Stretch;
    }

    private void SetSelectedMode(string mode)
    {
        if (ObjectTypeComboBox.SelectedItem as string == mode)
        {
            return;
        }

        _isUpdatingModeSelection = true;
        ObjectTypeComboBox.SelectedItem = mode;
        _isUpdatingModeSelection = false;
    }

    private void UpdateHeader()
    {
        bool hasProfiles = ConnectedProfiles.Count > 0;
        ProfileComboBox.IsEnabled = hasProfiles;
        ObjectTypeComboBox.IsEnabled = hasProfiles;
        RefreshModeButton.IsEnabled = hasProfiles;
        ActiveProfileSummaryTextBlock.Text = _sessionManager.SelectedSession is null
            ? "No database selected."
            : BuildActiveProfileSummary(_sessionManager.SelectedSession);
        ConnectionSummaryTextBlock.Text = _sessionManager.SelectedSession is null
            ? "No active connection"
            : BuildConnectionSummary(_sessionManager.SelectedSession);
        LastRefreshSummaryTextBlock.Text = BuildLastRefreshSummary(ObjectStatuses);
        if (!hasProfiles)
        {
            ConnectedProfilesSummaryTextBlock.Text = "No active database sessions.";
        }

        UpdateWorkspaceStatusSummary();
    }

    private static string GetModeSummary(string mode)
    {
        return mode switch
        {
            AppPackageMode => "Packages and entries",
            AllObjectsMode => "Search-first workspace",
            AppEngineMode => "Programs and items",
            RecordMode => "Records and events",
            PageMode => "Pages and events",
            ComponentMode => "Components and events",
            _ => "Browse read-only PeopleCode objects."
        };
    }

    private static string BuildLastRefreshSummary(IEnumerable<PeopleCodeObjectStatusItem> statuses)
    {
        DateTimeOffset? lastLoaded = statuses
            .Where(status => status.LastLoadedAt.HasValue)
            .Select(status => status.LastLoadedAt)
            .Max();

        return lastLoaded.HasValue
            ? $"Last refresh: {lastLoaded.Value.ToLocalTime():h:mm tt}"
            : "Last refresh: not loaded";
    }

    private void RebindWorkspaceStatusSubscriptions()
    {
        if (_trackedStatusCollection is not null)
        {
            _trackedStatusCollection.CollectionChanged -= TrackedStatusCollection_CollectionChanged;
        }

        foreach (PeopleCodeObjectStatusItem statusItem in _trackedStatusItems)
        {
            statusItem.PropertyChanged -= StatusItem_PropertyChanged;
        }

        _trackedStatusItems.Clear();
        _trackedStatusCollection = ObjectStatuses as INotifyCollectionChanged;
        if (_trackedStatusCollection is not null)
        {
            _trackedStatusCollection.CollectionChanged += TrackedStatusCollection_CollectionChanged;
        }

        foreach (PeopleCodeObjectStatusItem statusItem in ObjectStatuses)
        {
            statusItem.PropertyChanged += StatusItem_PropertyChanged;
            _trackedStatusItems.Add(statusItem);
        }
    }

    private void TrackedStatusCollection_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebindWorkspaceStatusSubscriptions();
        UpdateHeader();
    }

    private void StatusItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        UpdateHeader();
    }

    private void UpdateWorkspaceStatusSummary()
    {
        if (ActiveWorkspace is null)
        {
            BuildObjectStatusBar();
            return;
        }

        IReadOnlyList<string> statusParts = ActiveWorkspace.StatusStore.Items
            .Select(status => $"{status.ObjectTypeName} {status.StatusText.ToLowerInvariant()}")
            .ToList();

        BuildObjectStatusBar();
    }

    private void BuildObjectStatusBar()
    {
        ObjectStatusBarPanel.Children.Clear();

        foreach (PeopleCodeObjectStatusItem status in ObjectStatuses)
        {
            Border statusChip = new()
            {
                Background = Application.Current.Resources["LayerFillColorDefaultBrush"] as Brush,
                BorderBrush = Application.Current.Resources["CardStrokeColorDefaultBrush"] as Brush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8, 4, 4, 4),
                VerticalAlignment = VerticalAlignment.Center
            };

            StackPanel contentPanel = new()
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                VerticalAlignment = VerticalAlignment.Center
            };

            TextBlock nameTextBlock = new()
            {
                Text = status.ObjectTypeName,
                VerticalAlignment = VerticalAlignment.Center
            };
            nameTextBlock.Style = Application.Current.Resources["CaptionTextBlockStyle"] as Style;

            FontIcon stateIcon = new()
            {
                Glyph = GetStatusGlyph(status),
                FontSize = 10,
                Foreground = GetStatusBrush(status),
                VerticalAlignment = VerticalAlignment.Center
            };

            TextBlock stateTextBlock = new()
            {
                Text = GetCompactStatusLabel(status),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Application.Current.Resources["TextFillColorSecondaryBrush"] as Brush
            };
            stateTextBlock.Style = Application.Current.Resources["CaptionTextBlockStyle"] as Style;

            Button refreshButton = new()
            {
                MinWidth = 26,
                Height = 24,
                Padding = new Thickness(4, 0, 4, 0),
                Tag = status.ObjectTypeName,
                IsEnabled = status.CanRefresh,
                VerticalAlignment = VerticalAlignment.Center,
                Content = new FontIcon
                {
                    Glyph = "\uE72C",
                    FontSize = 10
                }
            };
            refreshButton.Click += RefreshStatusChipButton_Click;

            contentPanel.Children.Add(nameTextBlock);
            contentPanel.Children.Add(stateIcon);
            contentPanel.Children.Add(stateTextBlock);
            contentPanel.Children.Add(refreshButton);
            statusChip.Child = contentPanel;

            ToolTipService.SetToolTip(statusChip, BuildStatusToolTip(status));
            ToolTipService.SetToolTip(
                refreshButton,
                $"{BuildStatusToolTip(status)}\nClick to refresh {status.ObjectTypeName} metadata.");

            ObjectStatusBarPanel.Children.Add(statusChip);
        }
    }

    private async void RefreshStatusChipButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string objectType })
        {
            return;
        }

        await RefreshObjectTypeAsync(objectType);
        UpdateHeader();
        UpdateWorkspaceStatusSummary();
    }

    private string BuildStatusToolTip(PeopleCodeObjectStatusItem status)
    {
        string profileContext = _sessionManager.SelectedSession is null
            ? string.Empty
            : $"\nProfile: {_sessionManager.SelectedSession.DisplayName}";
        return $"{status.ObjectTypeName}\nStatus: {status.StatusText}\n{status.LastLoadedDisplayText}{profileContext}";
    }

    private static string GetCompactStatusLabel(PeopleCodeObjectStatusItem status)
    {
        return status.StatusText switch
        {
            "Loaded" => "Loaded",
            "Loading..." => "Loading",
            "Error" => "Error",
            _ => "Not loaded"
        };
    }

    private static string GetStatusGlyph(PeopleCodeObjectStatusItem status)
    {
        return status.StatusText switch
        {
            "Loaded" => "\uE73E",
            "Loading..." => "\uE895",
            "Error" => "\uEA39",
            _ => "\uE711"
        };
    }

    private static Brush? GetStatusBrush(PeopleCodeObjectStatusItem status)
    {
        return status.StatusText switch
        {
            "Loaded" => Application.Current.Resources["SystemFillColorSuccessBrush"] as Brush,
            "Loading..." => Application.Current.Resources["SystemFillColorCautionBrush"] as Brush,
            "Error" => Application.Current.Resources["SystemFillColorCriticalBrush"] as Brush,
            _ => Application.Current.Resources["TextFillColorDisabledBrush"] as Brush
        };
    }

    private static bool IsControlPressed()
    {
        CoreVirtualKeyStates controlState = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);
        return (controlState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
    }

    private sealed class ProfileWorkspace
    {
        public ProfileWorkspace(
            OracleConnectionSession session,
            DetachedSourceWindowManager detachedSourceWindowManager,
            PeopleCodeCompareWindowManager compareWindowManager)
        {
            Session = session;
            StatusStore = new PeopleCodeObjectStatusStore();
            AllObjectsView = new AllObjectsPeopleCodeBrowserView(detachedSourceWindowManager, compareWindowManager);
            AppEngineView = new AppEnginePlaceholderView(detachedSourceWindowManager, compareWindowManager);
            AppPackageView = new AppPackageBrowserView(detachedSourceWindowManager, compareWindowManager);
            RecordView = new RecordPeopleCodeBrowserView(detachedSourceWindowManager, compareWindowManager);
            PageView = new PagePeopleCodeBrowserView(detachedSourceWindowManager, compareWindowManager);
            ComponentView = new ComponentPeopleCodeBrowserView(detachedSourceWindowManager, compareWindowManager);
            AppPackageView.SetStatusStore(StatusStore);
            AppEngineView.SetStatusStore(StatusStore);
            RecordView.SetStatusStore(StatusStore);
            PageView.SetStatusStore(StatusStore);
            ComponentView.SetStatusStore(StatusStore);
            SetSession(session);
        }

        public OracleConnectionSession Session { get; private set; }

        public string CurrentMode { get; set; } = AppPackageMode;

        public PeopleCodeObjectStatusStore StatusStore { get; }

        private AllObjectsPeopleCodeBrowserView AllObjectsView { get; }

        private AppEnginePlaceholderView AppEngineView { get; }

        private AppPackageBrowserView AppPackageView { get; }

        private RecordPeopleCodeBrowserView RecordView { get; }

        private PagePeopleCodeBrowserView PageView { get; }

        private ComponentPeopleCodeBrowserView ComponentView { get; }

        public UIElement GetContentForMode(string mode)
        {
            CurrentMode = mode;
            return mode switch
            {
                AllObjectsMode => AllObjectsView,
                AppEngineMode => AppEngineView,
                RecordMode => RecordView,
                PageMode => PageView,
                ComponentMode => ComponentView,
                _ => AppPackageView
            };
        }

        public Task RefreshAsync(string objectType)
        {
            return objectType switch
            {
                AppPackageMode => AppPackageView.RefreshAsync(),
                AppEngineMode => AppEngineView.RefreshAsync(),
                RecordMode => RecordView.RefreshAsync(),
                PageMode => PageView.RefreshAsync(),
                ComponentMode => ComponentView.RefreshAsync(),
                _ => Task.CompletedTask
            };
        }

        public void SetSession(OracleConnectionSession session)
        {
            Session = session;
            AllObjectsView.SetSession(session);
            AppPackageView.SetSession(session);
            AppEngineView.SetSession(session);
            RecordView.SetSession(session);
            PageView.SetSession(session);
            ComponentView.SetSession(session);
        }

        public Task<bool> OpenItemAsync(string mode, object sourceKey)
        {
            return mode switch
            {
                AppPackageMode when sourceKey is AppPackageEntry entry => AppPackageView.OpenEntryAsync(entry),
                AppEngineMode when sourceKey is AppEngineItem item => AppEngineView.OpenItemAsync(item),
                RecordMode when sourceKey is RecordPeopleCodeItem item => RecordView.OpenItemAsync(item),
                PageMode when sourceKey is PagePeopleCodeItem item => PageView.OpenItemAsync(item),
                ComponentMode when sourceKey is ComponentPeopleCodeItem item => ComponentView.OpenItemAsync(item),
                _ => Task.FromResult(false)
            };
        }

        public void FocusGlobalSearch()
        {
            switch (CurrentMode)
            {
                case AllObjectsMode:
                    AllObjectsView.FocusGlobalSearch();
                    break;
                case AppEngineMode:
                    AppEngineView.FocusGlobalSearch();
                    break;
                case RecordMode:
                    RecordView.FocusGlobalSearch();
                    break;
                case PageMode:
                    PageView.FocusGlobalSearch();
                    break;
                case ComponentMode:
                    ComponentView.FocusGlobalSearch();
                    break;
                default:
                    AppPackageView.FocusGlobalSearch();
                    break;
            }
        }
    }
}

internal static class OracleConnectionSessionCollectionExtensions
{
    public static void UpsertSession(
        this ObservableCollection<OracleConnectionSession> sessions,
        OracleConnectionSession session)
    {
        OracleConnectionSession? existing = sessions.FirstOrDefault(existingSession =>
            existingSession.ProfileId.Equals(session.ProfileId, StringComparison.OrdinalIgnoreCase));

        if (existing is null)
        {
            sessions.Add(session);
            return;
        }

        int existingIndex = sessions.IndexOf(existing);
        sessions[existingIndex] = session;
    }
}

