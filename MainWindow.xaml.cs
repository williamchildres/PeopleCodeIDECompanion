using System;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using PeopleCodeIDECompanion.Models;
using Microsoft.UI.Xaml;
using Windows.UI;
using WinRT.Interop;

namespace PeopleCodeIDECompanion;

public sealed partial class MainWindow : Window
{
    private const string BaseTitle = "PeopleCodeIDECompanion";
    private readonly AppWindow _appWindow;

    public MainWindow()
    {
        InitializeComponent();
        _appWindow = GetAppWindowForCurrentWindow();
        Title = BaseTitle;
        Activated += MainWindow_Activated;
    }

    public void UpdateConnectionTitle(OracleConnectionSession? session, int activeSessionCount = 0)
    {
        Title = BaseTitle;
    }

    public void MinimizeWindow()
    {
        if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.Minimize();
        }
    }

    private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        Activated -= MainWindow_Activated;
        ConfigureCustomTitleBar();
    }

    private void ConfigureCustomTitleBar()
    {
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(ShellView.TitleBarDragRegion);

        if (!AppWindowTitleBar.IsCustomizationSupported())
        {
            return;
        }

        AppWindowTitleBar titleBar = _appWindow.TitleBar;
        Color foreground = Color.FromArgb(0xFF, 0xF3, 0xF3, 0xF3);
        Color hoverBackground = Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF);
        Color pressedBackground = Color.FromArgb(0x1F, 0xFF, 0xFF, 0xFF);
        Color inactiveForeground = Color.FromArgb(0x99, 0xF3, 0xF3, 0xF3);

        titleBar.ButtonBackgroundColor = Colors.Transparent;
        titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        titleBar.ButtonForegroundColor = foreground;
        titleBar.ButtonInactiveForegroundColor = inactiveForeground;
        titleBar.ButtonHoverBackgroundColor = hoverBackground;
        titleBar.ButtonPressedBackgroundColor = pressedBackground;
        titleBar.ButtonHoverForegroundColor = foreground;
        titleBar.ButtonPressedForegroundColor = foreground;
    }

    private AppWindow GetAppWindowForCurrentWindow()
    {
        nint hwnd = WindowNative.GetWindowHandle(this);
        WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        return AppWindow.GetFromWindowId(windowId);
    }
}
