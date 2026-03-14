using Microsoft.UI.Xaml.Controls;
using PeopleCodeIDECompanion.Models;

namespace PeopleCodeIDECompanion.Views;

public sealed partial class PeopleCodeInterfaceView : UserControl
{
    private const string AppPackageMode = "App Package";
    private const string AppEngineMode = "App Engine";

    private readonly AppEnginePlaceholderView _appEngineView = new();
    private readonly AppPackageBrowserView _appPackageView = new();
    private bool _isUpdatingModeSelection;

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
        SetSelectedMode(AppPackageMode);
        ModeSummaryTextBlock.Text = "Browse App Package classes and search App Package PeopleCode with the current read-only tools.";
        ModeContentHost.Content = _appPackageView;
    }

    public void ShowAppEngine()
    {
        SetSelectedMode(AppEngineMode);
        ModeSummaryTextBlock.Text = "Browse read-only App Engine PeopleCode by program, section, step, and action, and search source text with the current Oracle-backed tools.";
        ModeContentHost.Content = _appEngineView;
    }

    private void ObjectTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingModeSelection)
        {
            return;
        }

        if (ObjectTypeComboBox.SelectedItem as string == AppEngineMode)
        {
            ShowAppEngine();
            return;
        }

        ShowAppPackage();
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
}
