using Microsoft.UI.Xaml;

namespace PeopleCodeIDECompanion;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    public static Window? MainWindow { get; private set; }

    public App()
    {
        StartupDiagnostics.WriteBreadcrumb("app-ctor-entered");
        InitializeComponent();
        StartupDiagnostics.WriteBreadcrumb("app-initializecomponent-complete");
        UnhandledException += OnUnhandledException;
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        StartupDiagnostics.WriteBreadcrumb("onlaunched-entered");
        MainWindow = new MainWindow();
        StartupDiagnostics.WriteBreadcrumb("mainwindow-constructed");
        MainWindow.Activate();
        StartupDiagnostics.WriteBreadcrumb("mainwindow-activated");
    }

    private static void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        StartupDiagnostics.Log(e.Exception, "App.UnhandledException");
    }
}
