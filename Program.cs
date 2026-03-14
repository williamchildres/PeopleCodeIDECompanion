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
        try
        {
            ComWrappersSupport.InitializeComWrappers();
            Application.Start(_ =>
            {
                try
                {
                    var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                    SynchronizationContext.SetSynchronizationContext(context);
                    new App();
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
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PeopleCodeIDECompanion",
        "startup.log");

    public static void Log(Exception exception, string stage)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);

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
}
