using System.Collections.Generic;
using Microsoft.UI.Xaml.Controls;

namespace PeopleCodeIDECompanion.Views;

public sealed partial class ReadOnlyPlaceholderModeView : UserControl
{
    public ReadOnlyPlaceholderModeView(PlaceholderModeConfiguration configuration)
    {
        InitializeComponent();
        ApplyConfiguration(configuration);
    }

    private void ApplyConfiguration(PlaceholderModeConfiguration configuration)
    {
        ModeTitleTextBlock.Text = configuration.ModeTitle;
        ModeSubtitleTextBlock.Text = configuration.ModeSubtitle;
        ModeDescriptionTextBlock.Text = configuration.ModeDescription;

        BrowsePaneTitleTextBlock.Text = configuration.BrowsePaneTitle;
        BrowseSearchTextBox.PlaceholderText = configuration.BrowseSearchPlaceholder;
        BrowseListView.ItemsSource = configuration.BrowsePaneSamples;
        BrowsePaneHintTextBlock.Text = configuration.BrowsePaneHint;

        ChildPaneTitleTextBlock.Text = configuration.ChildPaneTitle;
        ChildSearchTextBox.PlaceholderText = configuration.ChildSearchPlaceholder;
        ChildListView.ItemsSource = configuration.ChildPaneSamples;
        ChildPaneHintTextBlock.Text = configuration.ChildPaneHint;

        MetadataTitleTextBlock.Text = configuration.MetadataTitle;
        MetadataSummaryTextBlock.Text = configuration.MetadataSummary;

        SourcePaneTitleTextBlock.Text = configuration.SourcePaneTitle;
        SourcePreviewTextBlock.Text = configuration.SourcePreviewText;
    }
}

public sealed record PlaceholderModeConfiguration(
    string ModeTitle,
    string ModeSubtitle,
    string ModeDescription,
    string BrowsePaneTitle,
    string BrowseSearchPlaceholder,
    IReadOnlyList<string> BrowsePaneSamples,
    string BrowsePaneHint,
    string ChildPaneTitle,
    string ChildSearchPlaceholder,
    IReadOnlyList<string> ChildPaneSamples,
    string ChildPaneHint,
    string MetadataTitle,
    string MetadataSummary,
    string SourcePaneTitle,
    string SourcePreviewText);
