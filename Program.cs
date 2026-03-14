using System;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using WinRT;

namespace PeopleCodeIDECompanion;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        StartupDiagnostics.WriteBreadcrumb("main-entered");

        try
        {
            ComWrappersSupport.InitializeComWrappers();
            StartupDiagnostics.WriteBreadcrumb("com-wrappers-initialized");

            Application.Start(_ =>
            {
                StartupDiagnostics.WriteBreadcrumb("application-start-entered");

                try
                {
                    var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                    SynchronizationContext.SetSynchronizationContext(context);
                    StartupDiagnostics.WriteBreadcrumb("sync-context-set");
                    new App();
                    StartupDiagnostics.WriteBreadcrumb("app-constructed");
                }
                catch (Exception ex)
                {
                    StartupDiagnostics.Log(ex, "Application.Start");
                    throw;
                }
            });
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log(ex, "Program.Main");
            throw;
        }
    }
}

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
