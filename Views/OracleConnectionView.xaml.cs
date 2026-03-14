using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PeopleCodeIDECompanion.Models;
using PeopleCodeIDECompanion.Services;

namespace PeopleCodeIDECompanion.Views;

public sealed partial class OracleConnectionView : UserControl
{
    private readonly OracleConnectionTester _connectionTester = new();
    private readonly SavedOracleConnectionStore _savedConnectionStore = new();
    private OracleConnectionSession? _lastSuccessfulSession;

    public OracleConnectionView()
    {
        InitializeComponent();
        Loaded += OracleConnectionView_Loaded;
    }

    public event EventHandler<OracleConnectionSession>? BrowserRequested;

    public ObservableCollection<SavedOracleConnectionProfile> SavedConnections { get; } = [];

    public SavedOracleConnectionProfile? SelectedSavedConnection { get; private set; }

    private async void OracleConnectionView_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OracleConnectionView_Loaded;
        await RefreshSavedConnectionsAsync();
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
        _lastSuccessfulSession = result.IsSuccess
            ? new OracleConnectionSession
            {
                DisplayName = DisplayNameTextBox.Text.Trim(),
                Options = options
            }
            : null;
        OpenBrowserButton.IsEnabled = result.IsSuccess;
        TestConnectionButton.Content = "Test Connection";
        TestConnectionButton.IsEnabled = true;
    }

    private async void SaveConnectionButton_Click(object sender, RoutedEventArgs e)
    {
        InlineErrorInfoBar.IsOpen = false;

        string displayName = DisplayNameTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            ShowInlineError("Enter a profile name before saving.");
            return;
        }

        SavedOracleConnectionProfile profile = new()
        {
            DisplayName = displayName,
            Host = HostTextBox.Text.Trim(),
            Port = PortTextBox.Text.Trim(),
            ServiceName = ServiceNameTextBox.Text.Trim(),
            Username = UsernameTextBox.Text.Trim()
        };

        await _savedConnectionStore.SaveAsync(profile);
        await RefreshSavedConnectionsAsync(displayName);
        StatusSummaryTextBlock.Text = "Saved connection profile";
        StatusDetailsTextBox.Text = $"Saved profile \"{displayName}\" locally. Passwords are not stored.";
    }

    private void LoadSavedConnectionButton_Click(object sender, RoutedEventArgs e)
    {
        InlineErrorInfoBar.IsOpen = false;

        if (SelectedSavedConnection is null)
        {
            ShowInlineError("Select a saved connection to load.");
            return;
        }

        DisplayNameTextBox.Text = SelectedSavedConnection.DisplayName;
        HostTextBox.Text = SelectedSavedConnection.Host;
        PortTextBox.Text = SelectedSavedConnection.Port;
        ServiceNameTextBox.Text = SelectedSavedConnection.ServiceName;
        UsernameTextBox.Text = SelectedSavedConnection.Username;
        PasswordBoxControl.Password = string.Empty;

        ResetSuccessfulConnection();
        StatusSummaryTextBlock.Text = "Saved connection loaded";
        StatusDetailsTextBox.Text = "Connection details were loaded from the local JSON file. Re-enter the password before connecting.";
    }

    private async void DeleteSavedConnectionButton_Click(object sender, RoutedEventArgs e)
    {
        InlineErrorInfoBar.IsOpen = false;

        if (SelectedSavedConnection is null)
        {
            ShowInlineError("Select a saved connection to delete.");
            return;
        }

        string displayName = SelectedSavedConnection.DisplayName;
        await _savedConnectionStore.DeleteAsync(displayName);
        await RefreshSavedConnectionsAsync();
        StatusSummaryTextBlock.Text = "Saved connection deleted";
        StatusDetailsTextBox.Text = $"Deleted profile \"{displayName}\" from the local JSON file.";
    }

    private void OpenBrowserButton_Click(object sender, RoutedEventArgs e)
    {
        InlineErrorInfoBar.IsOpen = false;

        if (_lastSuccessfulSession is null)
        {
            ShowInlineError("Test the connection successfully before opening the App Package browser.");
            return;
        }

        BrowserRequested?.Invoke(this, _lastSuccessfulSession);
    }

    private void SavedConnectionsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SelectedSavedConnection = SavedConnectionsComboBox.SelectedItem as SavedOracleConnectionProfile;
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
            Port = PortTextBox.Text,
            ServiceName = ServiceNameTextBox.Text,
            Username = UsernameTextBox.Text,
            Password = PasswordBoxControl.Password
        };
    }

    private async Task RefreshSavedConnectionsAsync(string? selectedDisplayName = null)
    {
        SavedConnections.Clear();
        SavedConnectionsComboBox.Items.Clear();

        foreach (SavedOracleConnectionProfile profile in await _savedConnectionStore.LoadAsync())
        {
            SavedConnections.Add(profile);
            SavedConnectionsComboBox.Items.Add(profile);
        }

        if (!string.IsNullOrWhiteSpace(selectedDisplayName))
        {
            foreach (SavedOracleConnectionProfile profile in SavedConnections)
            {
                if (profile.DisplayName.Equals(selectedDisplayName, StringComparison.OrdinalIgnoreCase))
                {
                    SavedConnectionsComboBox.SelectedItem = profile;
                    SelectedSavedConnection = profile;
                    return;
                }
            }
        }

        SavedConnectionsComboBox.SelectedItem = null;
        SelectedSavedConnection = null;
    }

    private void ResetSuccessfulConnection()
    {
        _lastSuccessfulSession = null;
        OpenBrowserButton.IsEnabled = false;
    }

    private void ShowInlineError(string message)
    {
        InlineErrorInfoBar.Message = message;
        InlineErrorInfoBar.IsOpen = true;
    }
}
