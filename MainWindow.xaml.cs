using System;
using PeopleCodeIDECompanion.Models;
using Microsoft.UI.Xaml;

namespace PeopleCodeIDECompanion;

public sealed partial class MainWindow : Window
{
    private const string BaseTitle = "PeopleCodeIDECompanion";
    private const int MaxTitleLength = 140;

    public MainWindow()
    {
        InitializeComponent();
        Title = BaseTitle;
    }

    public void UpdateConnectionTitle(OracleConnectionSession? session)
    {
        if (session is null)
        {
            Title = BaseTitle;
            return;
        }

        string profilePart = string.IsNullOrWhiteSpace(session.DisplayName)
            ? "Connected"
            : $"Connected with profile {session.DisplayName}";
        string endpoint = $"{session.Options.Host}:{session.Options.Port}/{session.Options.ServiceName}";
        string fullTitle = $"{BaseTitle} - {profilePart} as {session.Options.Username} to {endpoint}";

        Title = fullTitle.Length <= MaxTitleLength
            ? fullTitle
            : $"{BaseTitle} - {profilePart} as {session.Options.Username} to {TruncateMiddle(endpoint, 44)}";
    }

    private static string TruncateMiddle(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        if (maxLength <= 3)
        {
            return value[..maxLength];
        }

        int visibleLength = maxLength - 3;
        int startLength = (int)Math.Ceiling(visibleLength / 2d);
        int endLength = visibleLength - startLength;
        return $"{value[..startLength]}...{value[^endLength..]}";
    }
}
