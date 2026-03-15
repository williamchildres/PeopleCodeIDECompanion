using System;
using System.IO;
using System.Text;
namespace PeopleCodeIDECompanion;

internal static class StartupDiagnostics
{
    private static readonly string BreadcrumbDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PeopleCodeIDECompanion");

    private static readonly string LogPath = Path.Combine(
        BreadcrumbDirectory,
        "startup.log");

    private static readonly string BreadcrumbPath = Path.Combine(BreadcrumbDirectory, "startup.marker");

    public static void Log(Exception exception, string stage)
    {
        try
        {
            Directory.CreateDirectory(BreadcrumbDirectory);

            var builder = new StringBuilder();
            builder.AppendLine($"Timestamp: {DateTimeOffset.Now:O}");
            builder.AppendLine($"Stage: {stage}");
            builder.AppendLine($"ExceptionType: {exception.GetType().FullName}");
            builder.AppendLine($"Message: {exception.Message}");
            builder.AppendLine("StackTrace:");
            builder.AppendLine(exception.ToString());
            builder.AppendLine();

            File.AppendAllText(LogPath, builder.ToString(), Encoding.UTF8);
        }
        catch
        {
            // Swallow logging failures so startup exceptions preserve their original behavior.
        }
    }

    public static void WriteBreadcrumb(string stage)
    {
        try
        {
            Directory.CreateDirectory(BreadcrumbDirectory);
            File.AppendAllText(
                BreadcrumbPath,
                $"{DateTimeOffset.Now:O} {stage}{Environment.NewLine}",
                Encoding.UTF8);
        }
        catch
        {
            // Swallow breadcrumb failures so startup behavior stays unchanged.
        }
    }
}
