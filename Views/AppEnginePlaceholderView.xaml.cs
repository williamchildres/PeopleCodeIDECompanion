using Microsoft.UI.Xaml.Controls;
using PeopleCodeIDECompanion.Models;

namespace PeopleCodeIDECompanion.Views;

public sealed partial class AppEnginePlaceholderView : UserControl
{
    public AppEnginePlaceholderView()
    {
        InitializeComponent();
    }

    public void SetSession(OracleConnectionSession session)
    {
        string profileLabel = string.IsNullOrWhiteSpace(session.DisplayName)
            ? "the active Oracle session"
            : $"profile {session.DisplayName}";
        ConnectionStateTextBlock.Text =
            $"Ready to use {profileLabel} when App Engine queries are added. No App Engine database calls run in this placeholder mode.";
    }
}
