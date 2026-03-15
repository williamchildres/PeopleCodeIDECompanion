using System;
using PeopleCodeIDECompanion.Models;

namespace PeopleCodeIDECompanion.Services;

public static class DetachedPeopleCodeSourceContextFactory
{
    private const int MaxWindowTitleLength = 160;

    public static DetachedPeopleCodeSourceContext Create(
        OracleConnectionSession? session,
        string objectType,
        string objectTitle,
        string objectSubtitle,
        string metadataSummary,
        string lastUpdatedText,
        string sourceText,
        string? searchText,
        bool useSyntaxHighlighting,
        PeopleCodeSourceIdentity? sourceIdentity = null,
        PeopleCodeAuthoringCapabilitySnapshot? authoringCapabilities = null)
    {
        string trimmedTitle = objectTitle?.Trim() ?? string.Empty;
        string trimmedSubtitle = objectSubtitle?.Trim() ?? string.Empty;
        string windowTitle = BuildWindowTitle(objectType, trimmedTitle, trimmedSubtitle);

        return new DetachedPeopleCodeSourceContext
        {
            WindowTitle = windowTitle,
            ObjectType = objectType?.Trim() ?? string.Empty,
            ObjectTitle = trimmedTitle,
            ObjectSubtitle = trimmedSubtitle,
            ProfileContext = BuildProfileContext(session),
            MetadataSummary = metadataSummary?.Trim() ?? string.Empty,
            LastUpdatedText = lastUpdatedText?.Trim() ?? string.Empty,
            SourceText = sourceText ?? string.Empty,
            SearchText = string.IsNullOrWhiteSpace(searchText) ? null : searchText.Trim(),
            UseSyntaxHighlighting = useSyntaxHighlighting,
            SourceIdentity = sourceIdentity ?? new PeopleCodeSourceIdentity
            {
                ProfileId = session?.ProfileId ?? string.Empty,
                ObjectType = objectType?.Trim() ?? string.Empty,
                ObjectTitle = trimmedTitle
            },
            AuthoringCapabilities = authoringCapabilities ?? new PeopleCodeAuthoringCapabilitySnapshot()
        };
    }

    private static string BuildProfileContext(OracleConnectionSession? session)
    {
        if (session is null)
        {
            return "No database selected.";
        }

        return $"{session.DisplayName} | {session.Options.Username} @ {session.Options.Host}:{session.Options.Port}/{session.Options.ServiceName}";
    }

    private static string BuildWindowTitle(string objectType, string objectTitle, string objectSubtitle)
    {
        string primary = string.IsNullOrWhiteSpace(objectTitle) ? objectType : $"{objectType}: {objectTitle}";
        string fullTitle = string.IsNullOrWhiteSpace(objectSubtitle)
            ? primary
            : $"{primary} - {objectSubtitle}";

        if (fullTitle.Length <= MaxWindowTitleLength)
        {
            return fullTitle;
        }

        return $"{primary[..Math.Min(primary.Length, 80)]} - {TruncateMiddle(objectSubtitle, 72)}";
    }

    private static string TruncateMiddle(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
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
