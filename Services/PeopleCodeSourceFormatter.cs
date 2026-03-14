using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace PeopleCodeIDECompanion.Services;

internal static partial class PeopleCodeSourceFormatter
{
    private static readonly Regex TokenRegex = BuildTokenRegex();
    private static readonly Brush CommentBrush = CreateBrush("#7DC18A");
    private static readonly Brush StringBrush = CreateBrush("#E6B86A");
    private static readonly Brush BuiltInBrush = CreateBrush("#7BC6F4");
    private static readonly Brush KeywordBrush = CreateBrush("#7FB4FF");
    private static readonly Brush MatchBackgroundBrush = CreateBrush("#5A4700");
    private static readonly Brush MatchForegroundBrush = CreateBrush("#FFF4B8");
    private static readonly Brush ActiveMatchBackgroundBrush = CreateBrush("#C48A00");
    private static readonly Brush ActiveMatchForegroundBrush = CreateBrush("#1A1200");

    public static void ApplyFormatting(
        RichTextBlock sourceViewer,
        string sourceText,
        bool useSyntaxHighlighting,
        string? highlightedSearchText,
        int activeMatchIndex = -1)
    {
        sourceText ??= string.Empty;
        sourceViewer.Blocks.Clear();
        sourceViewer.TextHighlighters.Clear();

        Paragraph paragraph = new();
        Brush defaultForeground = ResolveBrush("TextFillColorPrimaryBrush");
        Match[] tokenMatches = useSyntaxHighlighting
            ? TokenRegex.Matches(sourceText).Cast<Match>().Where(static match => match.Success).ToArray()
            : [];

        foreach ((int start, int length, Match? tokenMatch) in BuildSegments(sourceText, tokenMatches))
        {
            Run run = new()
            {
                Text = sourceText.Substring(start, length),
                Foreground = defaultForeground
            };

            if (tokenMatch is not null)
            {
                if (tokenMatch.Groups["Comment"].Success)
                {
                    run.Foreground = CommentBrush;
                }
                else if (tokenMatch.Groups["String"].Success)
                {
                    run.Foreground = StringBrush;
                }
                else if (tokenMatch.Groups["BuiltIn"].Success)
                {
                    run.Foreground = BuiltInBrush;
                }
                else if (tokenMatch.Groups["Keyword"].Success)
                {
                    run.Foreground = KeywordBrush;
                    run.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
                }
            }

            paragraph.Inlines.Add(run);
        }

        sourceViewer.Blocks.Add(paragraph);

        if (!string.IsNullOrWhiteSpace(highlightedSearchText) && !string.IsNullOrEmpty(sourceText))
        {
            ApplyMatchHighlighting(sourceViewer, sourceText, highlightedSearchText, activeMatchIndex);
        }
    }

    public static IReadOnlyList<TextRange> GetMatchRanges(string sourceText, string highlightedSearchText)
    {
        List<TextRange> ranges = [];

        if (string.IsNullOrWhiteSpace(highlightedSearchText) || string.IsNullOrEmpty(sourceText))
        {
            return ranges;
        }

        int currentIndex = 0;
        while (currentIndex < sourceText.Length)
        {
            int matchIndex = sourceText.IndexOf(highlightedSearchText, currentIndex, StringComparison.OrdinalIgnoreCase);
            if (matchIndex < 0)
            {
                break;
            }

            ranges.Add(new TextRange(matchIndex, highlightedSearchText.Length));
            currentIndex = matchIndex + Math.Max(1, highlightedSearchText.Length);
        }

        return ranges;
    }

    private static void ApplyMatchHighlighting(
        RichTextBlock sourceViewer,
        string sourceText,
        string highlightedSearchText,
        int activeMatchIndex)
    {
        IReadOnlyList<TextRange> matchRanges = GetMatchRanges(sourceText, highlightedSearchText);
        if (matchRanges.Count == 0)
        {
            return;
        }

        TextHighlighter highlighter = new()
        {
            Background = MatchBackgroundBrush,
            Foreground = MatchForegroundBrush
        };

        for (int index = 0; index < matchRanges.Count; index++)
        {
            if (index == activeMatchIndex)
            {
                continue;
            }

            highlighter.Ranges.Add(matchRanges[index]);
        }

        if (highlighter.Ranges.Count > 0)
        {
            sourceViewer.TextHighlighters.Add(highlighter);
        }

        if (activeMatchIndex >= 0 && activeMatchIndex < matchRanges.Count)
        {
            TextHighlighter activeHighlighter = new()
            {
                Background = ActiveMatchBackgroundBrush,
                Foreground = ActiveMatchForegroundBrush
            };
            activeHighlighter.Ranges.Add(matchRanges[activeMatchIndex]);
            sourceViewer.TextHighlighters.Add(activeHighlighter);
        }
    }

    private static int[] BuildBoundaries(string sourceText, Match[] tokenMatches)
    {
        var boundaries = new System.Collections.Generic.HashSet<int> { 0, sourceText.Length };
        if (tokenMatches.Length == 0 || string.IsNullOrEmpty(sourceText))
        {
            return boundaries.OrderBy(static value => value).ToArray();
        }

        foreach (Match match in tokenMatches)
        {
            boundaries.Add(match.Index);
            boundaries.Add(match.Index + match.Length);
        }

        return boundaries.OrderBy(static value => value).ToArray();
    }

    private static System.Collections.Generic.IEnumerable<(int Start, int Length, Match? TokenMatch)> BuildSegments(string sourceText, Match[] tokenMatches)
    {
        int[] boundaries = BuildBoundaries(sourceText, tokenMatches);
        int tokenIndex = 0;
        for (int index = 0; index < boundaries.Length - 1; index++)
        {
            int start = boundaries[index];
            int end = boundaries[index + 1];
            if (end > start)
            {
                while (tokenIndex < tokenMatches.Length && tokenMatches[tokenIndex].Index + tokenMatches[tokenIndex].Length <= start)
                {
                    tokenIndex++;
                }

                Match? tokenMatch = tokenIndex < tokenMatches.Length &&
                                    tokenMatches[tokenIndex].Index <= start &&
                                    start < tokenMatches[tokenIndex].Index + tokenMatches[tokenIndex].Length
                    ? tokenMatches[tokenIndex]
                    : null;

                yield return (start, end - start, tokenMatch);
            }
        }
    }

    private static Brush ResolveBrush(string resourceKey)
    {
        if (Application.Current.Resources.TryGetValue(resourceKey, out object? resource) && resource is Brush brush)
        {
            return brush;
        }

        return CreateBrush("#FFFFFF");
    }

    private static Brush CreateBrush(string colorHex)
    {
        return (Brush)Microsoft.UI.Xaml.Markup.XamlReader.Load(
            $"<SolidColorBrush xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" Color=\"{colorHex}\" />");
    }

    [GeneratedRegex("""
(?<Comment>/\*[\s\S]*?\*/)|(?<String>"(?:[^"]|"")*"|'(?:[^']|'')*')|(?<Keyword>\b(?:Local|Global|Component|Property|If|Then|Else|ElseIf|End-If|For|To|Step|End-For|While|End-While|Repeat|Until|Evaluate|When|When-Other|End-Evaluate|Method|End-Method|Class|End-Class|Function|End-Function|Declare|Import|Returns|Try|Catch|End-Try|Create|Instance|Constant|Return|Break|Continue|Exit|Warning|Error|Rem)\b)|(?<BuiltIn>\b(?:CreateRecord|CreateSQL|CreateArray|CreateObject|GetRecord|GetField|GetFile|MessageBox|WinMessage|None|Null|NullValue|All|Substring|Len|Upper|Lower|Value|Round|Date|Time|DateTimeToLocalizedString)\b)
""", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex BuildTokenRegex();
}
