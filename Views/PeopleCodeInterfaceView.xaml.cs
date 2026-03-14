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

    private readonly AllObjectsPeopleCodeBrowserView _allObjectsView = new();
    private readonly AppEnginePlaceholderView _appEngineView = new();
    private readonly AppPackageBrowserView _appPackageView = new();
    private readonly RecordPeopleCodeBrowserView _recordView = new();
    private readonly PagePeopleCodeBrowserView _pageView = new();
    private readonly ComponentPeopleCodeBrowserView _componentView = new();
    private bool _isUpdatingModeSelection;

    public PeopleCodeInterfaceView()
    {
        InitializeComponent();
        ShowAppPackage();
    }

    public void SetSession(OracleConnectionSession session)
    {
        _allObjectsView.SetSession(session);
        _appPackageView.SetSession(session);
        _appEngineView.SetSession(session);
        _recordView.SetSession(session);
        _pageView.SetSession(session);
        _componentView.SetSession(session);
    }

    public void ShowAppPackage()
    {
        SetSelectedMode(AppPackageMode);
        ModeSummaryTextBlock.Text = "Browse App Package classes and search App Package PeopleCode with the current read-only tools.";
        ModeContentHost.Content = _appPackageView;
    }

    public void ShowAllObjects()
    {
        SetSelectedMode(AllObjectsMode);
        ModeSummaryTextBlock.Text = "Search across all supported read-only PeopleCode object types from one workspace, then inspect grouped matches, metadata, and source without browsing the full corpus.";
        ModeContentHost.Content = _allObjectsView;
    }

    public void ShowAppEngine()
    {
        SetSelectedMode(AppEngineMode);
        ModeSummaryTextBlock.Text = "Browse read-only App Engine PeopleCode by program, section, step, and action, and search source text with the current Oracle-backed tools.";
        ModeContentHost.Content = _appEngineView;
    }

    public void ShowRecord()
    {
        SetSelectedMode(RecordMode);
        ModeSummaryTextBlock.Text = "Browse read-only Record PeopleCode by record, field, and event, and search source text across the current field-event Record subset.";
        ModeContentHost.Content = _recordView;
    }

    public void ShowPage()
    {
        SetSelectedMode(PageMode);
        ModeSummaryTextBlock.Text = "Browse read-only Page PeopleCode by page and page-scoped item/event key, and search Page source text across the current Oracle-backed OBJECTID1=9 subset.";
        ModeContentHost.Content = _pageView;
    }

    public void ShowComponent()
    {
        SetSelectedMode(ComponentMode);
        ModeSummaryTextBlock.Text = "Browse read-only Component PeopleCode by component and item/event within the verified Oracle-backed OBJECTID1=10 subset, and search source text across that same subset.";
        ModeContentHost.Content = _componentView;
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
