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

public sealed partial class PeopleCodeOverviewView : UserControl
{
    private const int RecentItemLimit = 150;
    private const int RecentAuthorLimit = 50;
    private const int OpridDetailLimit = 100;

    private readonly OracleSessionManager _sessionManager;
    private readonly PeopleCodeOverviewService _overviewService = new();
    private readonly ObservableCollection<OracleConnectionSession> _connectedProfiles = [];
    private readonly ObservableCollection<PeopleCodeOverviewItem> _recentUpdates = [];
    private readonly ObservableCollection<PeopleCodeAuthorActivityItem> _recentAuthors = [];
    private readonly ObservableCollection<PeopleCodeOverviewItem> _detailItems = [];
    private readonly ObservableCollection<PeopleCodeRepeatedCodeBlock> _repeatedBlocks = [];
    private readonly ObservableCollection<PeopleCodeRepeatedCodeOccurrence> _repeatedOccurrences = [];

    private bool _isUpdatingProfileSelection;

    public PeopleCodeOverviewView(OracleSessionManager sessionManager)
    {
        _sessionManager = sessionManager;
        InitializeComponent();

        ProfileComboBox.ItemsSource = _connectedProfiles;
        RecentObjectsListView.ItemsSource = _recentUpdates;
        OpridsListView.ItemsSource = _recentAuthors;
        DetailItemsListView.ItemsSource = _detailItems;
        RepeatedBlocksListView.ItemsSource = _repeatedBlocks;
        RepeatedOccurrencesListView.ItemsSource = _repeatedOccurrences;

        _sessionManager.SessionsChanged += SessionManager_SessionsChanged;
        _sessionManager.SelectedSessionChanged += SessionManager_SelectedSessionChanged;

        LookbackComboBox.SelectedIndex = 1;
        SyncProfiles();
        UpdateHeader();
    }

    public event EventHandler<PeopleCodeObjectNavigationRequest>? NavigateToPeopleCodeObjectRequested;

    private OracleConnectionSession? SelectedProfile => ProfileComboBox.SelectedItem as OracleConnectionSession;

    private TimeSpan SelectedLookbackWindow => TimeSpan.FromDays(GetSelectedLookbackDays());

    private void SessionManager_SessionsChanged(object? sender, EventArgs e)
    {
        SyncProfiles();
    }

    private void SessionManager_SelectedSessionChanged(object? sender, OracleConnectionSession? session)
    {
        SyncProfileSelection(session);
        UpdateHeader();
    }

    private async void ProfileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingProfileSelection || ProfileComboBox.SelectedItem is not OracleConnectionSession session)
        {
            return;
        }

        _sessionManager.SelectSession(session.ProfileId);
        await RefreshAsync();
    }

    private async void LookbackComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SelectedProfile is null)
        {
            return;
        }

        await RefreshAsync();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshAsync();
    }

    private async void RepeatedCodeSearchButton_Click(object sender, RoutedEventArgs e)
    {
        await RunRepeatedCodeSearchAsync();
    }

    private void OpridsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (OpridsListView.SelectedItem is not PeopleCodeAuthorActivityItem author || SelectedProfile is null)
        {
            return;
        }

        _ = LoadOpridDetailAsync(SelectedProfile, author.Oprid);
    }

    private void RecentObjectsListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is PeopleCodeOverviewItem item)
        {
            NavigateToItem(item);
        }
    }

    private void DetailItemsListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is PeopleCodeOverviewItem item)
        {
            NavigateToItem(item);
        }
    }

    private void RepeatedBlocksListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _repeatedOccurrences.Clear();
        if (RepeatedBlocksListView.SelectedItem is not PeopleCodeRepeatedCodeBlock block)
        {
            return;
        }

        foreach (PeopleCodeRepeatedCodeOccurrence occurrence in block.Occurrences)
        {
            _repeatedOccurrences.Add(occurrence);
        }
    }

    private void RepeatedOccurrencesListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is PeopleCodeRepeatedCodeOccurrence occurrence)
        {
            NavigateToItem(occurrence.Location);
        }
    }

    public async Task RefreshAsync()
    {
        OracleConnectionSession? profile = SelectedProfile ?? _sessionManager.SelectedSession;
        if (profile is null)
        {
            ClearData();
            UpdateHeader();
            return;
        }

        SetBusyState(isBusy: true);
        ClearMessages();
        ClearDetailPanels();

        PeopleCodeOverviewDataResult<PeopleCodeOverviewItem> updatesResult = await _overviewService.GetRecentPeopleCodeUpdatesAsync(
            profile,
            SelectedLookbackWindow,
            RecentItemLimit);

        PeopleCodeOverviewDataResult<PeopleCodeAuthorActivityItem> authorsResult = await _overviewService.GetRecentPeopleCodeAuthorsAsync(
            profile,
            SelectedLookbackWindow,
            RecentAuthorLimit);

        _recentUpdates.ReplaceWith(updatesResult.Items);
        _recentAuthors.ReplaceWith(authorsResult.Items);

        if (_recentAuthors.Count > 0)
        {
            OpridsListView.SelectedIndex = 0;
        }
        else
        {
            DetailTitleTextBlock.Text = "OPRID Detail";
            DetailStatusTextBlock.Text = "Select an OPRID to view recent PeopleCode updates.";
            DetailStatusTextBlock.Visibility = Visibility.Visible;
        }

        RecentObjectsStatusTextBlock.Text = _recentUpdates.Count == 0
            ? "No recent PeopleCode updates matched the current profile and lookback window."
            : string.Empty;
        RecentObjectsStatusTextBlock.Visibility = string.IsNullOrWhiteSpace(RecentObjectsStatusTextBlock.Text)
            ? Visibility.Collapsed
            : Visibility.Visible;

        OpridStatusTextBlock.Text = _recentAuthors.Count == 0
            ? "No recent OPRID activity matched the current profile and lookback window."
            : string.Empty;
        OpridStatusTextBlock.Visibility = string.IsNullOrWhiteSpace(OpridStatusTextBlock.Text)
            ? Visibility.Collapsed
            : Visibility.Visible;

        ApplyHeaderMessage(updatesResult.ErrorMessage, authorsResult.ErrorMessage, updatesResult.WarningMessage, authorsResult.WarningMessage);

        SetBusyState(isBusy: false);
        UpdateHeader();
    }

    private async Task LoadOpridDetailAsync(OracleConnectionSession profile, string oprid)
    {
        DetailTitleTextBlock.Text = $"OPRID Detail: {oprid}";
        DetailStatusTextBlock.Text = "Loading recent updates for the selected OPRID...";
        DetailStatusTextBlock.Visibility = Visibility.Visible;
        _detailItems.Clear();
        RepeatedResultsHeaderTextBlock.Visibility = Visibility.Collapsed;
        RepeatedCodePanel.Visibility = Visibility.Collapsed;

        PeopleCodeOverviewDataResult<PeopleCodeOverviewItem> result = await _overviewService.GetRecentUpdatesByOpridAsync(
            profile,
            oprid,
            SelectedLookbackWindow,
            OpridDetailLimit);

        _detailItems.ReplaceWith(result.Items);
        DetailStatusTextBlock.Text = _detailItems.Count == 0
            ? "No recent updates were found for the selected OPRID in the current lookback window."
            : string.Empty;
        DetailStatusTextBlock.Visibility = string.IsNullOrWhiteSpace(DetailStatusTextBlock.Text)
            ? Visibility.Collapsed
            : Visibility.Visible;

        ApplyHeaderMessage(result.ErrorMessage, string.Empty, result.WarningMessage);
    }

    private async Task RunRepeatedCodeSearchAsync()
    {
        OracleConnectionSession? profile = SelectedProfile ?? _sessionManager.SelectedSession;
        if (profile is null)
        {
            return;
        }

        DetailTitleTextBlock.Text = "Repeated Code Search";
        DetailStatusTextBlock.Text = "Scanning PeopleCode source blocks...";
        DetailStatusTextBlock.Visibility = Visibility.Visible;
        _detailItems.Clear();
        _repeatedBlocks.Clear();
        _repeatedOccurrences.Clear();
        RepeatedResultsHeaderTextBlock.Visibility = Visibility.Visible;
        RepeatedCodePanel.Visibility = Visibility.Visible;
        RepeatedBlocksListView.SelectedItem = null;

        PeopleCodeRepeatedCodeSearchResult result = await _overviewService.FindRepeatedCodeBlocksAsync(
            profile,
            "All supported objects",
            new PeopleCodeRepeatedCodeSearchOptions());

        _repeatedBlocks.ReplaceWith(result.Blocks);
        if (_repeatedBlocks.Count > 0)
        {
            RepeatedBlocksListView.SelectedIndex = 0;
        }

        DetailStatusTextBlock.Text = !string.IsNullOrWhiteSpace(result.ErrorMessage)
            ? result.ErrorMessage
            : _repeatedBlocks.Count == 0
                ? "No repeated normalized code blocks met the current thresholds."
                : result.Summary;
        DetailStatusTextBlock.Visibility = Visibility.Visible;

        ApplyHeaderMessage(result.ErrorMessage, string.Empty, result.WarningMessage);
    }

    private void NavigateToItem(PeopleCodeOverviewItem item)
    {
        OracleConnectionSession? profile = SelectedProfile ?? _sessionManager.SelectedSession;
        if (profile is null || item.SourceKey is null)
        {
            return;
        }

        NavigateToPeopleCodeObjectRequested?.Invoke(
            this,
            new PeopleCodeObjectNavigationRequest
            {
                ProfileId = profile.ProfileId,
                ObjectType = item.ObjectType,
                SourceKey = item.SourceKey
            });
    }

    private void SyncProfiles()
    {
        foreach (OracleConnectionSession session in _sessionManager.Sessions)
        {
            _connectedProfiles.UpsertSession(session);
        }

        foreach (OracleConnectionSession removedSession in _connectedProfiles
                     .Where(existing => _sessionManager.Sessions.All(session =>
                         !session.ProfileId.Equals(existing.ProfileId, StringComparison.OrdinalIgnoreCase)))
                     .ToList())
        {
            _connectedProfiles.Remove(removedSession);
        }

        SyncProfileSelection(_sessionManager.SelectedSession);
        if ((_sessionManager.SelectedSession ?? SelectedProfile) is not null && _recentUpdates.Count == 0 && _recentAuthors.Count == 0)
        {
            _ = RefreshAsync();
        }
    }

    private void SyncProfileSelection(OracleConnectionSession? session)
    {
        _isUpdatingProfileSelection = true;
        ProfileComboBox.SelectedItem = session is null
            ? null
            : _connectedProfiles.FirstOrDefault(connectedProfile =>
                connectedProfile.ProfileId.Equals(session.ProfileId, StringComparison.OrdinalIgnoreCase));
        _isUpdatingProfileSelection = false;
    }

    private void UpdateHeader()
    {
        OracleConnectionSession? session = SelectedProfile ?? _sessionManager.SelectedSession;
        bool hasProfiles = _connectedProfiles.Count > 0;

        ProfileComboBox.IsEnabled = hasProfiles;
        LookbackComboBox.IsEnabled = hasProfiles;
        RefreshButton.IsEnabled = hasProfiles;
        RepeatedCodeSearchButton.IsEnabled = hasProfiles;

        ActiveProfileSummaryTextBlock.Text = session is null
            ? "No database selected."
            : $"{session.DisplayName} | {session.Options.Username} @ {session.Options.Host}:{session.Options.Port}/{session.Options.ServiceName}";

        SummaryTextBlock.Text = hasProfiles
            ? "Review recent PeopleCode activity, active OPRIDs, and bounded repeated-code findings for the selected profile."
            : "Connect to at least one Oracle profile to view PeopleCode overview data.";
    }

    private void SetBusyState(bool isBusy)
    {
        if (isBusy)
        {
            RecentObjectsStatusTextBlock.Text = "Loading recent PeopleCode updates...";
            RecentObjectsStatusTextBlock.Visibility = Visibility.Visible;
            OpridStatusTextBlock.Text = "Loading recent OPRID activity...";
            OpridStatusTextBlock.Visibility = Visibility.Visible;
        }

        RefreshButton.IsEnabled = !isBusy && _connectedProfiles.Count > 0;
        RepeatedCodeSearchButton.IsEnabled = !isBusy && _connectedProfiles.Count > 0;
    }

    private void ClearMessages()
    {
        HeaderInfoBar.IsOpen = false;
        HeaderInfoBar.Message = string.Empty;
        RecentObjectsStatusTextBlock.Text = string.Empty;
        RecentObjectsStatusTextBlock.Visibility = Visibility.Collapsed;
        OpridStatusTextBlock.Text = string.Empty;
        OpridStatusTextBlock.Visibility = Visibility.Collapsed;
    }

    private void ClearData()
    {
        _recentUpdates.Clear();
        _recentAuthors.Clear();
        ClearDetailPanels();
    }

    private void ClearDetailPanels()
    {
        _detailItems.Clear();
        _repeatedBlocks.Clear();
        _repeatedOccurrences.Clear();
        DetailTitleTextBlock.Text = "OPRID Detail";
        DetailStatusTextBlock.Text = "Select an OPRID to view recent PeopleCode updates.";
        DetailStatusTextBlock.Visibility = Visibility.Visible;
        RepeatedResultsHeaderTextBlock.Visibility = Visibility.Collapsed;
        RepeatedCodePanel.Visibility = Visibility.Collapsed;
    }

    private void ApplyHeaderMessage(params string[] messages)
    {
        string[] errors = messages.Take(2).Where(message => !string.IsNullOrWhiteSpace(message)).ToArray();
        string[] warnings = messages.Skip(2).Where(message => !string.IsNullOrWhiteSpace(message)).ToArray();

        if (errors.Length > 0)
        {
            HeaderInfoBar.Severity = InfoBarSeverity.Error;
            HeaderInfoBar.Message = string.Join(" | ", errors);
            HeaderInfoBar.IsOpen = true;
            return;
        }

        if (warnings.Length > 0)
        {
            HeaderInfoBar.Severity = InfoBarSeverity.Warning;
            HeaderInfoBar.Message = string.Join(" | ", warnings);
            HeaderInfoBar.IsOpen = true;
        }
    }

    private int GetSelectedLookbackDays()
    {
        return LookbackComboBox.SelectedItem is ComboBoxItem { Tag: string tag } && int.TryParse(tag, out int days)
            ? days
            : 14;
    }
}

internal static class ObservableCollectionExtensions
{
    public static void ReplaceWith<T>(this ObservableCollection<T> collection, IEnumerable<T> items)
    {
        collection.Clear();
        foreach (T item in items)
        {
            collection.Add(item);
        }
    }
}
