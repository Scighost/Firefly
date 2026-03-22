using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Vanara.PInvoke;
using Windows.Foundation;
using WinRT.Interop;


namespace Firefly;

[ObservableObject]
public sealed partial class TrayWindow : Window
{


    public nint WindowHandle => WindowNative.GetWindowHandle(this);


    public float UIScale => User32.GetDpiForWindow(WindowHandle) / 96f;




    public TrayWindow()
    {
        InitializeComponent();
        InitializeWindow();
        SetTrayIcon();
    }



    private unsafe void InitializeWindow()
    {
        AppWindow.IsShownInSwitchers = false;
        AppWindow.Closing += (s, e) => e.Cancel = true;
        this.Activated += TrayWindow_Activated;
        var flag = User32.GetWindowLongPtr(WindowHandle, User32.WindowLongFlags.GWL_STYLE);
        flag &= ~(nint)User32.WindowStyles.WS_CAPTION;
        flag &= ~(nint)User32.WindowStyles.WS_MAXIMIZEBOX;
        flag &= ~(nint)User32.WindowStyles.WS_MINIMIZEBOX;
        flag &= ~(nint)User32.WindowStyles.WS_SIZEBOX;
        User32.SetWindowLong(WindowHandle, User32.WindowLongFlags.GWL_STYLE, flag);
        User32.SetWindowPos(WindowHandle, HWND.HWND_TOPMOST, 0, 0, 0, 0, User32.SetWindowPosFlags.SWP_NOMOVE | User32.SetWindowPosFlags.SWP_NOSIZE);
        var p = DwmApi.DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUND;
        DwmApi.DwmSetWindowAttribute(WindowHandle, DwmApi.DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE, (nint)(&p), sizeof(DwmApi.DWM_WINDOW_CORNER_PREFERENCE));
        Show();
        Hide();
    }



    private void SetTrayIcon()
    {
        try
        {
            nint hInstance = Kernel32.GetModuleHandle(null).DangerousGetHandle();
            nint hIcon = User32.LoadIcon(hInstance, "#32512").DangerousGetHandle();
            trayIcon.Icon = System.Drawing.Icon.FromHandle(hIcon);
        }
        catch { }
    }



    private void TrayWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState is WindowActivationState.Deactivated)
        {
            Hide();
        }
    }



    [RelayCommand]
    public void Show()
    {
        RootGrid.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        SIZE windowSize = new()
        {
            Width = (int)(RootGrid.DesiredSize.Width * UIScale),
            Height = (int)(RootGrid.DesiredSize.Height * UIScale)
        };
        User32.GetCursorPos(out POINT point);
        User32.CalculatePopupWindowPosition(point, windowSize, User32.TrackPopupMenuFlags.TPM_RIGHTALIGN | User32.TrackPopupMenuFlags.TPM_BOTTOMALIGN | User32.TrackPopupMenuFlags.TPM_WORKAREA, null, out RECT windowPos);
        User32.MoveWindow(WindowHandle, windowPos.X, windowPos.Y, windowPos.Width, windowPos.Height, true);
        AppWindow?.Show(true);
        User32.SetForegroundWindow(WindowHandle);
    }



    public void Hide()
    {
        AppWindow?.Hide();
    }



    [RelayCommand]
    private void ShowMainWindow()
    {
        App.Current.ShowMainWindow();
    }


    [RelayCommand]
    private void ExitApp()
    {
        App.Current.Exit();
    }



}
