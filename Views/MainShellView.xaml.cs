using Microsoft.UI.Xaml.Controls;
using PeopleCodeIDECompanion.Models;

namespace PeopleCodeIDECompanion.Views;

public sealed partial class MainShellView : UserControl
{
    private readonly AppPackageBrowserView _appPackageBrowserView = new();
    private readonly OracleConnectionView _oracleConnectionView = new();
    private readonly ReferenceExplorerView _referenceExplorerView = new();

    public MainShellView()
    {
        InitializeComponent();

        _oracleConnectionView.BrowserRequested += OracleConnectionView_BrowserRequested;

        ContentHost.Content = _oracleConnectionView;
        AppNavigationView.SelectedItem = AppNavigationView.MenuItems[0];
    }

    private void OracleConnectionView_BrowserRequested(object? sender, OracleConnectionSession session)
    {
        _appPackageBrowserView.SetSession(session);
        AppPackageBrowserNavigationItem.IsEnabled = true;
        ContentHost.Content = _appPackageBrowserView;
        AppNavigationView.SelectedItem = AppPackageBrowserNavigationItem;
    }

    private void AppNavigationView_SelectionChanged(
        NavigationView sender,
        NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer?.Tag is not string destination)
        {
            return;
        }

        ContentHost.Content = destination switch
        {
            "OracleConnection" => _oracleConnectionView,
            "AppPackageBrowser" => _appPackageBrowserView,
            "ReferenceExplorer" => _referenceExplorerView,
            _ => _referenceExplorerView
        };
    }
}
