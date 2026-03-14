using System;
using System.Threading;
using PeopleCodeIDECompanion.Services;

namespace PeopleCodeIDECompanion.Models;

public sealed class PeopleCodeOverviewProfileSettings
{
    public const int DefaultObjectTypeTimeoutSeconds = 20;
    public const int InfiniteTimeoutSeconds = 0;
    public const int MinimumObjectTypeTimeoutSeconds = 1;
    public const int MaximumObjectTypeTimeoutSeconds = 300;

    public bool IgnorePplsoftModifiedObjects { get; set; }

    public int AppPackageTimeoutSeconds { get; set; } = DefaultObjectTypeTimeoutSeconds;

    public int AppEngineTimeoutSeconds { get; set; } = DefaultObjectTypeTimeoutSeconds;

    public int RecordTimeoutSeconds { get; set; } = DefaultObjectTypeTimeoutSeconds;

    public int PageTimeoutSeconds { get; set; } = DefaultObjectTypeTimeoutSeconds;

    public int ComponentTimeoutSeconds { get; set; } = DefaultObjectTypeTimeoutSeconds;

    public TimeSpan GetTimeout(string objectType)
    {
        int seconds = objectType switch
        {
            AllObjectsPeopleCodeBrowserService.AppPackageMode => AppPackageTimeoutSeconds,
            AllObjectsPeopleCodeBrowserService.AppEngineMode => AppEngineTimeoutSeconds,
            AllObjectsPeopleCodeBrowserService.RecordMode => RecordTimeoutSeconds,
            AllObjectsPeopleCodeBrowserService.PageMode => PageTimeoutSeconds,
            AllObjectsPeopleCodeBrowserService.ComponentMode => ComponentTimeoutSeconds,
            _ => DefaultObjectTypeTimeoutSeconds
        };

        return seconds == InfiniteTimeoutSeconds
            ? Timeout.InfiniteTimeSpan
            : TimeSpan.FromSeconds(seconds);
    }

    public static PeopleCodeOverviewProfileSettings Normalize(PeopleCodeOverviewProfileSettings? settings)
    {
        PeopleCodeOverviewProfileSettings source = settings ?? new PeopleCodeOverviewProfileSettings();
        return new PeopleCodeOverviewProfileSettings
        {
            IgnorePplsoftModifiedObjects = source.IgnorePplsoftModifiedObjects,
            AppPackageTimeoutSeconds = NormalizeTimeout(source.AppPackageTimeoutSeconds),
            AppEngineTimeoutSeconds = NormalizeTimeout(source.AppEngineTimeoutSeconds),
            RecordTimeoutSeconds = NormalizeTimeout(source.RecordTimeoutSeconds),
            PageTimeoutSeconds = NormalizeTimeout(source.PageTimeoutSeconds),
            ComponentTimeoutSeconds = NormalizeTimeout(source.ComponentTimeoutSeconds)
        };
    }

    private static int NormalizeTimeout(int value)
    {
        if (value == InfiniteTimeoutSeconds)
        {
            return InfiniteTimeoutSeconds;
        }

        if (value < 0)
        {
            return DefaultObjectTypeTimeoutSeconds;
        }

        return Math.Clamp(value, MinimumObjectTypeTimeoutSeconds, MaximumObjectTypeTimeoutSeconds);
    }
}
