using Microsoft.UI.Xaml;

namespace PeopleCodeIDECompanion;

public partial class App : Application
{
    public static Window? MainWindow { get; private set; }
    private Window? _window;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        MainWindow = new MainWindow();
        _window = MainWindow;
        _window.Activate();
    }
}
