using Microsoft.UI.Xaml.Controls;
using PeopleCodeIDECompanion.Models;

namespace PeopleCodeIDECompanion.Views;

public sealed partial class PeopleCodeInterfaceView : UserControl
{
    private readonly AppEnginePlaceholderView _appEngineView = new();
    private readonly AppPackageBrowserView _appPackageView = new();

    public PeopleCodeInterfaceView()
    {
        InitializeComponent();
        ShowAppPackage();
    }

    public void SetSession(OracleConnectionSession session)
    {
        _appPackageView.SetSession(session);
        _appEngineView.SetSession(session);
    }

    public void ShowAppPackage()
    {
        ModeSummaryTextBlock.Text = "Browse App Package classes and search App Package PeopleCode with the current read-only tools.";
        ModeContentHost.Content = _appPackageView;
    }

    public void ShowAppEngine()
    {
        ModeSummaryTextBlock.Text = "Prepare App Engine-specific browse and search workflows without changing the current runtime or query model yet.";
        ModeContentHost.Content = _appEngineView;
    }
}
