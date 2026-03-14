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
        AppPackageNavigationItem.IsEnabled = true;
        PeopleCodeInterfaceNavigationItem.IsExpanded = true;
        ContentHost.Content = _peopleCodeInterfaceView;
        AppNavigationView.SelectedItem = AppPackageNavigationItem;
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
            "PeopleCodeInterface.AppPackage" => ShowPeopleCodeInterfaceAppPackage(),
            "PeopleCodeInterface.AppEngine" => ShowPeopleCodeInterfaceAppEngine(),
            "ReferenceExplorer" => _referenceExplorerView,
            _ => _referenceExplorerView
        };
    }

    private PeopleCodeInterfaceView ShowPeopleCodeInterfaceAppPackage()
    {
        PeopleCodeInterfaceNavigationItem.IsExpanded = true;
        _peopleCodeInterfaceView.ShowAppPackage();
        return _peopleCodeInterfaceView;
    }

    private PeopleCodeInterfaceView ShowPeopleCodeInterfaceAppEngine()
    {
        PeopleCodeInterfaceNavigationItem.IsExpanded = true;
        _peopleCodeInterfaceView.ShowAppEngine();
        return _peopleCodeInterfaceView;
    }
}
