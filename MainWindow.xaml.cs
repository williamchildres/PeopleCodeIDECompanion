using System;
using System.Runtime.InteropServices;
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
    private const int GwlWndProc = -4;
    private const uint WmGetMinMaxInfo = 0x0024;
    private const int MinWindowWidth = 1120;
    private const int MinWindowHeight = 760;
    private readonly AppWindow _appWindow;
    private readonly nint _windowHandle;
    private readonly WndProcDelegate _windowProcDelegate;
    private nint _originalWindowProc;

    public MainWindow()
    {
        InitializeComponent();
        _windowHandle = WindowNative.GetWindowHandle(this);
        _appWindow = GetAppWindowForCurrentWindow();
        _windowProcDelegate = WindowProc;
        Title = BaseTitle;
        Activated += MainWindow_Activated;
        Closed += MainWindow_Closed;
        ApplyMinimumWindowSize();
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

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        if (_originalWindowProc != 0)
        {
            SetWindowLongPtr(_windowHandle, GwlWndProc, _originalWindowProc);
            _originalWindowProc = 0;
        }
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

    private void ApplyMinimumWindowSize()
    {
        nint newWindowProc = Marshal.GetFunctionPointerForDelegate(_windowProcDelegate);
        _originalWindowProc = SetWindowLongPtr(_windowHandle, GwlWndProc, newWindowProc);
    }

    // Keep the custom menu/title bar usable by preventing the window from being dragged below the shell's working size.
    private nint WindowProc(nint hwnd, uint message, nint wParam, nint lParam)
    {
        if (message == WmGetMinMaxInfo)
        {
            MINMAXINFO minMaxInfo = Marshal.PtrToStructure<MINMAXINFO>(lParam);
            minMaxInfo.ptMinTrackSize.X = MinWindowWidth;
            minMaxInfo.ptMinTrackSize.Y = MinWindowHeight;
            Marshal.StructureToPtr(minMaxInfo, lParam, true);
            return 0;
        }

        return CallWindowProc(_originalWindowProc, hwnd, message, wParam, lParam);
    }

    private AppWindow GetAppWindowForCurrentWindow()
    {
        WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(_windowHandle);
        return AppWindow.GetFromWindowId(windowId);
    }

    private delegate nint WndProcDelegate(nint hwnd, uint message, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public Point ptReserved;
        public Point ptMaxSize;
        public Point ptMaxPosition;
        public Point ptMinTrackSize;
        public Point ptMaxTrackSize;
    }

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtr(nint hWnd, int nIndex, nint newLong);

    [DllImport("user32.dll", EntryPoint = "CallWindowProcW")]
    private static extern nint CallWindowProc(nint previousWindowProc, nint hWnd, uint message, nint wParam, nint lParam);
}
