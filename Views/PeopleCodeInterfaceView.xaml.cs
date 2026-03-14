using Microsoft.UI.Xaml.Controls;
using PeopleCodeIDECompanion.Models;

namespace PeopleCodeIDECompanion.Views;

public sealed partial class PeopleCodeInterfaceView : UserControl
{
    private const string AppPackageMode = "App Package";
    private const string AppEngineMode = "App Engine";
    private const string RecordMode = "Record";
    private const string PageMode = "Page";
    private const string ComponentMode = "Component";

    private readonly AppEnginePlaceholderView _appEngineView = new();
    private readonly AppPackageBrowserView _appPackageView = new();
    private readonly RecordPeopleCodeBrowserView _recordView = new();
    private readonly ReadOnlyPlaceholderModeView _pageView = new(
        new PlaceholderModeConfiguration(
            ModeTitle: "Page PeopleCode",
            ModeSubtitle: "Page, control, and event browsing shell",
            ModeDescription: "This placeholder reserves the same three-pane workflow for Page objects so the UI stays consistent before real read-only browsing is added.",
            BrowsePaneTitle: "Pages",
            BrowseSearchPlaceholder: "Search pages",
            BrowsePaneSamples: ["JOB_DATA", "PERSONAL_DATA", "MY_CUSTOM_PAGE"],
            BrowsePaneHint: "The left browse pane will list pages and page definitions returned by the selected environment.",
            ChildPaneTitle: "Controls / Events",
            ChildSearchPlaceholder: "Search controls, events, or page items",
            ChildPaneSamples: ["DERIVED_NAME.FieldChange", "Level 1 Grid.RowInit", "Activate"],
            ChildPaneHint: "The middle pane will focus on page controls, component buffers, and event entry points.",
            MetadataTitle: "Selected page metadata",
            MetadataSummary: "Future read-only metadata will summarize page keys, control context, event names, and update audit details for the selected page item.",
            SourcePaneTitle: "PeopleCode Source Preview",
            SourcePreviewText: "/* Page mode placeholder */\r\n/* Read-only page browsing is coming next. */\r\n\r\n/* Planned focus: pages, controls, and page-event source preview. */"));
    private readonly ReadOnlyPlaceholderModeView _componentView = new(
        new PlaceholderModeConfiguration(
            ModeTitle: "Component PeopleCode",
            ModeSubtitle: "Component, page, and event browsing shell",
            ModeDescription: "This placeholder keeps the intended Component browsing experience visible now, without adding new Oracle queries or writeback paths in this step.",
            BrowsePaneTitle: "Components",
            BrowseSearchPlaceholder: "Search components",
            BrowsePaneSamples: ["JOB_DATA", "PERSONAL_DATA", "MY_WORKCENTER"],
            BrowsePaneHint: "The left browse pane will list components available for read-only browsing.",
            ChildPaneTitle: "Pages / Events",
            ChildSearchPlaceholder: "Search pages, actions, or component events",
            ChildPaneSamples: ["JOB_DATA.Activate", "JOB_DATA.SavePostChange", "PERSONAL_DATA.RowInit"],
            ChildPaneHint: "The middle pane will narrow to component pages, actions, and event-level PeopleCode entries.",
            MetadataTitle: "Selected component metadata",
            MetadataSummary: "Future read-only metadata will summarize market, portal, component structure, and the selected event context before source is shown.",
            SourcePaneTitle: "PeopleCode Source Preview",
            SourcePreviewText: "/* Component mode placeholder */\r\n/* Read-only component browsing is coming next. */\r\n\r\n/* Planned focus: components, pages, and component-event source preview. */"));
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
        _recordView.SetSession(session);
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

    public void ShowRecord()
    {
        SetSelectedMode(RecordMode);
        ModeSummaryTextBlock.Text = "Browse read-only Record PeopleCode by record, field, and event, and search source text across the current field-event Record subset.";
        ModeContentHost.Content = _recordView;
    }

    public void ShowPage()
    {
        SetSelectedMode(PageMode);
        ModeSummaryTextBlock.Text = "Preview the Page browsing shell with page-oriented search controls, pane layout, and read-only implementation notes while Oracle-backed browsing is added next.";
        ModeContentHost.Content = _pageView;
    }

    public void ShowComponent()
    {
        SetSelectedMode(ComponentMode);
        ModeSummaryTextBlock.Text = "Preview the Component browsing shell with component-oriented search controls, pane layout, and read-only implementation notes while Oracle-backed browsing is added next.";
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
