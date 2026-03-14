using Microsoft.UI.Xaml.Controls;
using PeopleCodeIDECompanion.Models;

namespace PeopleCodeIDECompanion.Views;

public sealed partial class MainShellView : UserControl
{
    private readonly PeopleCodeInterfaceView _peopleCodeInterfaceView = new();
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
        _peopleCodeInterfaceView.SetSession(session);
        _peopleCodeInterfaceView.ShowAppPackage();
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

        ContentHost.Content = destination switch
        {
            "OracleConnection" => _oracleConnectionView,
            "PeopleCodeInterface" => _peopleCodeInterfaceView,
            "ReferenceExplorer" => _referenceExplorerView,
            _ => _referenceExplorerView
        };
    }
}
