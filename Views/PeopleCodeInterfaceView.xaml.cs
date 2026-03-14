using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PeopleCodeIDECompanion.Models;
using PeopleCodeIDECompanion.Services;

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
    private readonly Dictionary<string, ProfileWorkspace> _workspaces = [];
    private bool _isUpdatingModeSelection;
    private bool _isUpdatingProfileSelection;

    public PeopleCodeInterfaceView(OracleSessionManager sessionManager)
    {
        _sessionManager = sessionManager;
        InitializeComponent();
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
            _workspaces[session.ProfileId] = new ProfileWorkspace(session);
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
        return await workspace.OpenItemAsync(mode, sourceKey);
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
                _workspaces[session.ProfileId] = new ProfileWorkspace(session);
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
            ModeContentHost.Content = null;
            ModeSummaryTextBlock.Text = "Connect to at least one Oracle profile to browse read-only PeopleCode objects.";
            ConnectedProfilesSummaryTextBlock.Text = ConnectedProfiles.Count == 0
                ? "No active database sessions."
                : $"{ConnectedProfiles.Count} active database session(s).";
            ActiveWorkspaceChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        SetSelectedMode(workspace.CurrentMode);
        ModeSummaryTextBlock.Text = GetModeSummary(workspace.CurrentMode);
        ConnectedProfilesSummaryTextBlock.Text = ConnectedProfiles.Count == 1
            ? "1 active database session."
            : $"{ConnectedProfiles.Count} active database sessions.";
        ActiveProfileSummaryTextBlock.Text = BuildActiveProfileSummary(workspace.Session);
        ModeContentHost.Content = workspace.GetContentForMode(workspace.CurrentMode);
        ActiveWorkspaceChanged?.Invoke(this, EventArgs.Empty);
    }

    private static string BuildActiveProfileSummary(OracleConnectionSession session)
    {
        return $"{session.DisplayName} | {session.Options.Username} @ {session.Options.Host}:{session.Options.Port}/{session.Options.ServiceName}";
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
        ActiveProfileSummaryTextBlock.Text = _sessionManager.SelectedSession is null
            ? "No database selected."
            : BuildActiveProfileSummary(_sessionManager.SelectedSession);
        if (!hasProfiles)
        {
            ConnectedProfilesSummaryTextBlock.Text = "No active database sessions.";
        }
    }

    private static string GetModeSummary(string mode)
    {
        return mode switch
        {
            AppPackageMode => "Browse App Package classes and search App Package PeopleCode with the current read-only tools.",
            AllObjectsMode => "Search across all supported read-only PeopleCode object types from one workspace, then inspect grouped matches, metadata, and source without browsing the full corpus.",
            AppEngineMode => "Browse read-only App Engine PeopleCode by program, section, step, and action, and search source text with the current Oracle-backed tools.",
            RecordMode => "Browse read-only Record PeopleCode by record, field, and event, and search source text across the current field-event Record subset.",
            PageMode => "Browse read-only Page PeopleCode by page and page-scoped item/event key, and search Page source text across the current Oracle-backed OBJECTID1=9 subset.",
            ComponentMode => "Browse read-only Component PeopleCode by component and item/event within the verified Oracle-backed OBJECTID1=10 subset, and search source text across that same subset.",
            _ => "Browse read-only PeopleCode objects."
        };
    }

    private sealed class ProfileWorkspace
    {
        public ProfileWorkspace(OracleConnectionSession session)
        {
            Session = session;
            StatusStore = new PeopleCodeObjectStatusStore();
            AllObjectsView = new AllObjectsPeopleCodeBrowserView();
            AppEngineView = new AppEnginePlaceholderView();
            AppPackageView = new AppPackageBrowserView();
            RecordView = new RecordPeopleCodeBrowserView();
            PageView = new PagePeopleCodeBrowserView();
            ComponentView = new ComponentPeopleCodeBrowserView();
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
