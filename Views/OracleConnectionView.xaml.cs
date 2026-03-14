using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PeopleCodeIDECompanion.Models;
using PeopleCodeIDECompanion.Services;

namespace PeopleCodeIDECompanion.Views;

public sealed partial class OracleConnectionView : UserControl
{
    private const string NewConnectionProfileId = "__new__";
    private readonly OracleConnectionTester _connectionTester = new();
    private readonly SavedOracleConnectionStore _savedConnectionStore = new();
    private readonly SecureCredentialStore _secureCredentialStore = new();
    private readonly SavedOracleConnectionProfile _newConnectionPlaceholder = new()
    {
        ProfileId = NewConnectionProfileId,
        DisplayName = "New"
    };
    private OracleConnectionSession? _lastSuccessfulSession;

    public OracleConnectionView()
    {
        InitializeComponent();
        SavedConnectionsComboBox.ItemsSource = SavedConnections;
        Loaded += OracleConnectionView_Loaded;
    }

    public event EventHandler<OracleConnectionSession>? BrowserRequested;

    public event EventHandler<ProfileSavedEventArgs>? ProfileSaved;

    public event EventHandler<ProfileDeletedEventArgs>? ProfileDeleted;

    public ObservableCollection<SavedOracleConnectionProfile> SavedConnections { get; } = [];

    public SavedOracleConnectionProfile? SelectedSavedConnection { get; private set; }

    private async void OracleConnectionView_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OracleConnectionView_Loaded;
        try
        {
            await RefreshSavedConnectionsAsync();
            PrepareNewConnection();
        }
        catch (Exception exception)
        {
            ShowInlineError($"Could not load saved connections: {exception.Message}");
        }
    }

    private async void TestConnectionButton_Click(object sender, RoutedEventArgs e)
    {
        InlineErrorInfoBar.IsOpen = false;
        TestConnectionButton.IsEnabled = false;
        TestConnectionButton.Content = "Testing...";
        StatusSummaryTextBlock.Text = "Testing connection...";
        StatusDetailsTextBox.Text = "Attempting to connect to the Oracle database.";

        OracleConnectionOptions options = BuildConnectionOptions();
        OracleConnectionTestResult result = await _connectionTester.TestConnectionAsync(options);

        StatusSummaryTextBlock.Text = result.IsSuccess ? "Connection succeeded" : "Connection failed";
        StatusDetailsTextBox.Text = result.Details;
        _lastSuccessfulSession = result.IsSuccess ? BuildConnectionSession(options) : null;
        OpenBrowserButton.IsEnabled = result.IsSuccess;
        TestConnectionButton.Content = "Test Connection";
        TestConnectionButton.IsEnabled = true;
    }

    private async void SaveConnectionButton_Click(object sender, RoutedEventArgs e)
    {
        InlineErrorInfoBar.IsOpen = false;

        try
        {
            string displayName = DisplayNameTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(displayName))
            {
                ShowInlineError("Enter a profile name before saving.");
                return;
            }

            SavedOracleConnectionProfile? existingProfile = ResolveCurrentProfile();
            string profileId = existingProfile?.ProfileId ?? Guid.NewGuid().ToString("N");
            string credentialTargetId = existingProfile?.CredentialTargetId
                ?? SavedOracleConnectionStore.CreateCredentialTargetId(profileId);

            bool hasExistingStoredPassword = await _secureCredentialStore.HasPasswordAsync(credentialTargetId);
            string password = PasswordBoxControl.Password;
            if (AutoLoginToggleSwitch.IsOn && string.IsNullOrWhiteSpace(password) && !hasExistingStoredPassword)
            {
                ShowInlineError("Store a password before enabling Auto Login.");
                return;
            }

            if (!string.IsNullOrWhiteSpace(password))
            {
                await _secureCredentialStore.SavePasswordAsync(credentialTargetId, password);
            }

            SavedOracleConnectionProfile profile = new()
            {
                ProfileId = profileId,
                DisplayName = displayName,
                Host = HostTextBox.Text.Trim(),
                Port = string.IsNullOrWhiteSpace(PortTextBox.Text) ? "1521" : PortTextBox.Text.Trim(),
                ServiceName = ServiceNameTextBox.Text.Trim(),
                Username = UsernameTextBox.Text.Trim(),
                AutoLoginEnabled = AutoLoginToggleSwitch.IsOn,
                CredentialTargetId = credentialTargetId,
                LastConnectedAt = existingProfile?.LastConnectedAt,
                OverviewSettings = BuildOverviewSettings()
            };

            await _savedConnectionStore.SaveAsync(profile);
            await RefreshSavedConnectionsAsync(profile.ProfileId);
            string? previousSessionProfileId = _lastSuccessfulSession?.ProfileId;
            UpdateLastSuccessfulSession(profile);
            ProfileSaved?.Invoke(this, new ProfileSavedEventArgs(profile, previousSessionProfileId, _lastSuccessfulSession));
            StatusSummaryTextBlock.Text = "Saved connection profile";
            StatusDetailsTextBox.Text = !string.IsNullOrWhiteSpace(password) || hasExistingStoredPassword
                ? $"Saved profile \"{displayName}\" locally and stored its password with Windows DPAPI protection."
                : $"Saved profile \"{displayName}\" locally without a stored password. Enter a password later to enable Auto Login.";
        }
        catch (Exception exception)
        {
            ShowInlineError($"Could not save the profile: {exception.Message}");
        }
    }

    private async void LoadSavedConnectionButton_Click(object sender, RoutedEventArgs e)
    {
        InlineErrorInfoBar.IsOpen = false;

        try
        {
            if (SelectedSavedConnection is null || IsNewPlaceholder(SelectedSavedConnection))
            {
                ShowInlineError("Select a saved connection to load.");
                return;
            }

            DisplayNameTextBox.Text = SelectedSavedConnection.DisplayName;
            HostTextBox.Text = SelectedSavedConnection.Host;
            PortTextBox.Text = SelectedSavedConnection.Port;
            ServiceNameTextBox.Text = SelectedSavedConnection.ServiceName;
            UsernameTextBox.Text = SelectedSavedConnection.Username;
            AutoLoginToggleSwitch.IsOn = SelectedSavedConnection.AutoLoginEnabled;
            ApplyOverviewSettings(SelectedSavedConnection.OverviewSettings);
            PasswordBoxControl.Password = await _secureCredentialStore.LoadPasswordAsync(SelectedSavedConnection.CredentialTargetId)
                ?? string.Empty;

            ResetSuccessfulConnection();
            StatusSummaryTextBlock.Text = "Saved connection loaded";
            StatusDetailsTextBox.Text = string.IsNullOrWhiteSpace(PasswordBoxControl.Password)
                ? "Profile metadata was loaded. No saved password was found for this profile."
                : "Profile metadata and the securely stored password were loaded for this profile.";
        }
        catch (Exception exception)
        {
            ShowInlineError($"Could not load the profile: {exception.Message}");
        }
    }

    private async void DeleteSavedConnectionButton_Click(object sender, RoutedEventArgs e)
    {
        InlineErrorInfoBar.IsOpen = false;

        try
        {
            if (SelectedSavedConnection is null || IsNewPlaceholder(SelectedSavedConnection))
            {
                ShowInlineError("Select a saved connection to delete.");
                return;
            }

            SavedOracleConnectionProfile profile = SelectedSavedConnection;
            await _savedConnectionStore.DeleteAsync(profile.ProfileId);
            await _secureCredentialStore.DeletePasswordAsync(profile.CredentialTargetId);
            if (_lastSuccessfulSession?.ProfileId.Equals(profile.ProfileId, StringComparison.OrdinalIgnoreCase) == true)
            {
                ResetSuccessfulConnection();
            }
            await RefreshSavedConnectionsAsync();
            ProfileDeleted?.Invoke(this, new ProfileDeletedEventArgs(profile.ProfileId));
            StatusSummaryTextBlock.Text = "Saved connection deleted";
            StatusDetailsTextBox.Text = $"Deleted profile \"{profile.DisplayName}\" and removed its securely stored password.";
        }
        catch (Exception exception)
        {
            ShowInlineError($"Could not delete the profile: {exception.Message}");
        }
    }

    private void OpenBrowserButton_Click(object sender, RoutedEventArgs e)
    {
        InlineErrorInfoBar.IsOpen = false;

        if (_lastSuccessfulSession is null)
        {
            ShowInlineError("Test the connection successfully before opening the PeopleCode Interface.");
            return;
        }

        BrowserRequested?.Invoke(this, _lastSuccessfulSession);
    }

    private void SavedConnectionsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SelectedSavedConnection = SavedConnectionsComboBox.SelectedItem as SavedOracleConnectionProfile;
        if (SelectedSavedConnection is not null && IsNewPlaceholder(SelectedSavedConnection))
        {
            PrepareNewConnection();
        }
    }

    private void ConnectionInputChanged(object sender, RoutedEventArgs e)
    {
        ResetSuccessfulConnection();
    }

    private void PasswordBoxControl_PasswordChanged(object sender, RoutedEventArgs e)
    {
        ResetSuccessfulConnection();
    }

    private OracleConnectionOptions BuildConnectionOptions()
    {
        return new OracleConnectionOptions
        {
            Host = HostTextBox.Text,
            Port = string.IsNullOrWhiteSpace(PortTextBox.Text) ? "1521" : PortTextBox.Text,
            ServiceName = ServiceNameTextBox.Text,
            Username = UsernameTextBox.Text,
            Password = PasswordBoxControl.Password
        };
    }

    private OracleConnectionSession BuildConnectionSession(OracleConnectionOptions options)
    {
        SavedOracleConnectionProfile? currentProfile = ResolveCurrentProfile();
        string sessionProfileId = currentProfile?.ProfileId
            ?? $"session:{CreateManualSessionKey(options, DisplayNameTextBox.Text)}";
        return new OracleConnectionSession
        {
            ProfileId = sessionProfileId,
            DisplayName = string.IsNullOrWhiteSpace(DisplayNameTextBox.Text)
                ? $"{options.Username}@{options.Host}"
                : DisplayNameTextBox.Text.Trim(),
            CredentialTargetId = currentProfile?.CredentialTargetId ?? string.Empty,
            Options = options,
            OverviewSettings = currentProfile is null
                ? BuildOverviewSettings()
                : PeopleCodeOverviewProfileSettings.Normalize(currentProfile.OverviewSettings)
        };
    }

    private SavedOracleConnectionProfile? ResolveCurrentProfile()
    {
        if (SelectedSavedConnection is not null && !IsNewPlaceholder(SelectedSavedConnection))
        {
            return SelectedSavedConnection;
        }

        string displayName = DisplayNameTextBox.Text.Trim();
        return SavedConnections.FirstOrDefault(profile =>
            profile.DisplayName.Equals(displayName, StringComparison.OrdinalIgnoreCase));
    }

    private async Task RefreshSavedConnectionsAsync(string? selectedProfileId = null)
    {
        SavedConnections.Clear();
        SavedConnections.Add(_newConnectionPlaceholder);

        foreach (SavedOracleConnectionProfile profile in await _savedConnectionStore.LoadAsync())
        {
            SavedConnections.Add(profile);
        }

        if (!string.IsNullOrWhiteSpace(selectedProfileId))
        {
            SavedOracleConnectionProfile? selectedProfile = SavedConnections.FirstOrDefault(profile =>
                profile.ProfileId.Equals(selectedProfileId, StringComparison.OrdinalIgnoreCase));
            if (selectedProfile is not null)
            {
                SavedConnectionsComboBox.SelectedItem = selectedProfile;
                SelectedSavedConnection = selectedProfile;
                return;
            }
        }

        SavedConnectionsComboBox.SelectedItem = _newConnectionPlaceholder;
        SelectedSavedConnection = _newConnectionPlaceholder;
    }

    private void ResetSuccessfulConnection()
    {
        _lastSuccessfulSession = null;
        OpenBrowserButton.IsEnabled = false;
    }

    private void UpdateLastSuccessfulSession(SavedOracleConnectionProfile profile)
    {
        if (_lastSuccessfulSession is null)
        {
            return;
        }

        _lastSuccessfulSession = new OracleConnectionSession
        {
            ProfileId = profile.ProfileId,
            DisplayName = profile.DisplayName,
            CredentialTargetId = profile.CredentialTargetId,
            Options = _lastSuccessfulSession.Options,
            OverviewSettings = PeopleCodeOverviewProfileSettings.Normalize(profile.OverviewSettings)
        };
    }

    private void ShowInlineError(string message)
    {
        InlineErrorInfoBar.Message = message;
        InlineErrorInfoBar.IsOpen = true;
    }

    private static string CreateManualSessionKey(OracleConnectionOptions options, string displayName)
    {
        string rawKey = string.Join(
            "|",
            displayName.Trim(),
            options.Host.Trim(),
            options.Port.Trim(),
            options.ServiceName.Trim(),
            options.Username.Trim());
        return rawKey.Replace(" ", string.Empty);
    }

    private void PrepareNewConnection()
    {
        DisplayNameTextBox.Text = string.Empty;
        HostTextBox.Text = string.Empty;
        PortTextBox.Text = "1521";
        ServiceNameTextBox.Text = string.Empty;
        UsernameTextBox.Text = string.Empty;
        PasswordBoxControl.Password = string.Empty;
        AutoLoginToggleSwitch.IsOn = false;
        ApplyOverviewSettings(new PeopleCodeOverviewProfileSettings());
        ResetSuccessfulConnection();
        StatusSummaryTextBlock.Text = "Ready";
        StatusDetailsTextBox.Text = "Enter connection details, then select Test Connection.";
    }

    private PeopleCodeOverviewProfileSettings BuildOverviewSettings()
    {
        return PeopleCodeOverviewProfileSettings.Normalize(new PeopleCodeOverviewProfileSettings
        {
            IgnorePplsoftModifiedObjects = IgnorePplsoftModifiedObjectsToggleSwitch.IsOn,
            AppPackageTimeoutSeconds = ParseTimeout(AppPackageTimeoutTextBox.Text),
            AppEngineTimeoutSeconds = ParseTimeout(AppEngineTimeoutTextBox.Text),
            RecordTimeoutSeconds = ParseTimeout(RecordTimeoutTextBox.Text),
            PageTimeoutSeconds = ParseTimeout(PageTimeoutTextBox.Text),
            ComponentTimeoutSeconds = ParseTimeout(ComponentTimeoutTextBox.Text)
        });
    }

    private void ApplyOverviewSettings(PeopleCodeOverviewProfileSettings? settings)
    {
        PeopleCodeOverviewProfileSettings normalized = PeopleCodeOverviewProfileSettings.Normalize(settings);
        IgnorePplsoftModifiedObjectsToggleSwitch.IsOn = normalized.IgnorePplsoftModifiedObjects;
        AppPackageTimeoutTextBox.Text = normalized.AppPackageTimeoutSeconds.ToString();
        AppEngineTimeoutTextBox.Text = normalized.AppEngineTimeoutSeconds.ToString();
        RecordTimeoutTextBox.Text = normalized.RecordTimeoutSeconds.ToString();
        PageTimeoutTextBox.Text = normalized.PageTimeoutSeconds.ToString();
        ComponentTimeoutTextBox.Text = normalized.ComponentTimeoutSeconds.ToString();
    }

    private static int ParseTimeout(string value)
    {
        return int.TryParse(value?.Trim(), out int seconds)
            ? seconds
            : PeopleCodeOverviewProfileSettings.DefaultObjectTypeTimeoutSeconds;
    }

    private static bool IsNewPlaceholder(SavedOracleConnectionProfile profile)
    {
        return profile.ProfileId.Equals(NewConnectionProfileId, StringComparison.Ordinal);
    }
}

public sealed class ProfileSavedEventArgs : EventArgs
{
    public ProfileSavedEventArgs(
        SavedOracleConnectionProfile profile,
        string? previousSessionProfileId,
        OracleConnectionSession? savedSession)
    {
        Profile = profile;
        PreviousSessionProfileId = previousSessionProfileId;
        SavedSession = savedSession;
    }

    public SavedOracleConnectionProfile Profile { get; }

    public string? PreviousSessionProfileId { get; }

    public OracleConnectionSession? SavedSession { get; }
}

public sealed class ProfileDeletedEventArgs : EventArgs
{
    public ProfileDeletedEventArgs(string profileId)
    {
        ProfileId = profileId;
    }

    public string ProfileId { get; }
}
