using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using System;
using System.IO;
using Vanara.PInvoke;
using Windows.Graphics;


namespace Firefly;

public sealed partial class Live2dWindow : Window
{
    private nint WindowHandle => (nint)AppWindow.Id.Value;

    private float DpiScale => User32.GetDpiForWindow(WindowHandle) / 96f;

    public bool Borderless { get; set; }


    private ComCtl32.SUBCLASSPROC SUBCLASSPROC;

    private DispatcherQueueTimer _timer;


    public Live2dWindow()
    {
        InitializeComponent();
        InitializeWindow();
        CreateTimer();
        this.Closed += Live2dWindow_Closed;
        string file = Path.Combine(AppContext.BaseDirectory, "model", "FileReferences_Moc_0.model3.json");
        if (File.Exists(file))
        {
            live2dPanel.LoadModel(Path.Combine(AppContext.BaseDirectory, "model"), "FileReferences_Moc_0");
        }
    }


    private void InitializeWindow()
    {
        SetIcon();
        this.SizeChanged += MainWindow_SizeChanged;
        SystemBackdrop = new TransparentBackdrop();
        AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
        AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
        AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        int width = (int)(616 * DpiScale);
        int height = (int)(448 * DpiScale);
        AppWindow.ResizeClient(new SizeInt32(width, height));
        SUBCLASSPROC = new ComCtl32.SUBCLASSPROC(SubclassProc);
        ComCtl32.SetWindowSubclass(WindowHandle, SUBCLASSPROC, 1, IntPtr.Zero);

    }


    private void SetIcon()
    {
        try
        {
            nint hInstance = Kernel32.GetModuleHandle(null).DangerousGetHandle();
            nint hIcon = User32.LoadIcon(hInstance, "#32512").DangerousGetHandle();
            AppWindow.SetIcon(Win32Interop.GetIconIdFromIcon(hIcon));
        }
        catch { }
    }


    private void CreateTimer()
    {
        _timer = DispatcherQueue.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(33);
        _timer.Tick += DispatcherQueueTimer_Tick;
        _timer.Start();
    }


    private IntPtr SubclassProc(HWND hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, nuint uIdSubclass, IntPtr dwRefData)
    {
        if (Borderless && uMsg == (uint)User32.WindowMessage.WM_NCHITTEST)
        {
            return (IntPtr)User32.HitTestValues.HTTRANSPARENT;
        }
        return ComCtl32.DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }


    private void Live2dWindow_Closed(object sender, WindowEventArgs args)
    {
        this.Closed -= Live2dWindow_Closed;
        this.SizeChanged -= MainWindow_SizeChanged;
        _timer.Tick -= DispatcherQueueTimer_Tick;
        _timer.Stop();
        _timer = null!;
        GridMenuFlyout.Items.Clear();
    }



    private POINT _lastCursorPos;

    private bool _hitTransparent;


    private void DispatcherQueueTimer_Tick(DispatcherQueueTimer sender, object args)
    {
        if (AppWindow is null)
        {
            return;
        }

        bool lButton = (User32.GetAsyncKeyState(User32.VK.VK_LBUTTON) & 0x8000) != 0;
        bool rButton = (User32.GetAsyncKeyState(User32.VK.VK_RBUTTON) & 0x8000) != 0;
        User32.GetCursorPos(out var pt);
        User32.GetWindowRect(WindowHandle, out var rt);
        float x = (pt.X - rt.X - rt.Width / 2f) / (rt.Width / 2f);
        float y = -(pt.Y - rt.Y - rt.Height / 2f) / (rt.Height / 2f);

        float fx = live2dPanel.ViewFlipped ? -x : x;
        if (Borderless && live2dPanel.LApp.Live2dManager.GetModelNum() > 0 && !live2dPanel.LApp.Live2dManager.HitAnyDrawable(fx, y))
        {
            if (!_hitTransparent)
            {
                User32.WindowStylesEx styleEx = (User32.WindowStylesEx)User32.GetWindowLong(WindowHandle, User32.WindowLongFlags.GWL_EXSTYLE);
                styleEx |= (User32.WindowStylesEx.WS_EX_TRANSPARENT | User32.WindowStylesEx.WS_EX_LAYERED);
                User32.SetWindowLong(WindowHandle, User32.WindowLongFlags.GWL_EXSTYLE, (nint)styleEx);
                _hitTransparent = true;
            }
        }
        else if (_hitTransparent)
        {
            User32.WindowStylesEx styleEx = (User32.WindowStylesEx)User32.GetWindowLong(WindowHandle, User32.WindowLongFlags.GWL_EXSTYLE);
            styleEx &= ~(User32.WindowStylesEx.WS_EX_TRANSPARENT | User32.WindowStylesEx.WS_EX_LAYERED);
            User32.SetWindowLong(WindowHandle, User32.WindowLongFlags.GWL_EXSTYLE, (nint)styleEx);
            _hitTransparent = false;
        }

        if (lButton || rButton)
        {
            bool moved = Math.Abs(pt.X - _lastCursorPos.X) + Math.Abs(pt.Y - _lastCursorPos.Y) > 6;
            if (moved)
            {
                _lastCursorPos = pt;
                live2dPanel.MouseDragged(Math.Clamp(x, -0.6f, 0.6f), Math.Clamp(y, -0.6f, 0.6f));
            }
        }
        else
        {
            live2dPanel.MouseDragged(0, 0);
            _lastCursorPos = pt;
        }
    }


    private void MainWindow_SizeChanged(object sender, WindowSizeChangedEventArgs args)
    {
        int l = (int)(48 * DpiScale);
        int width = (int)(args.Size.Width * DpiScale - l);
        AppWindow.TitleBar.SetDragRectangles([new RectInt32(l, 0, width, l)]);
    }



    private void MenuFlyoutItem_BorderlessWindow_Click(object sender, RoutedEventArgs e)
    {
        int l = (int)(8 * DpiScale);
        User32.GetWindowRect(WindowHandle, out var rt);
        User32.WindowStyles style = (User32.WindowStyles)User32.GetWindowLong(WindowHandle, User32.WindowLongFlags.GWL_STYLE);
        User32.WindowStylesEx styleEx = (User32.WindowStylesEx)User32.GetWindowLong(WindowHandle, User32.WindowLongFlags.GWL_EXSTYLE);
        if (Borderless)
        {
            Borderless = false;
            Border_DragArea.Visibility = Visibility.Visible;
            style |= User32.WindowStyles.WS_OVERLAPPEDWINDOW;
            styleEx &= ~(User32.WindowStylesEx.WS_EX_TOOLWINDOW);
            rt = new RECT(rt.X - l, rt.Y, rt.Right + l, rt.Bottom + l);
            MenuFlyoutItem_BorderlessWindow.Text = "无边框 + 置顶";
        }
        else
        {
            Borderless = true;
            Border_DragArea.Visibility = Visibility.Collapsed;
            style &= ~User32.WindowStyles.WS_OVERLAPPEDWINDOW;
            styleEx |= (User32.WindowStylesEx.WS_EX_TOOLWINDOW);
            rt = new RECT(rt.X + l, rt.Y, rt.Right - l, rt.Bottom - l);
            MenuFlyoutItem_BorderlessWindow.Text = "窗口模式";
        }
        User32.SetWindowLong(WindowHandle, User32.WindowLongFlags.GWL_STYLE, (nint)style);
        User32.SetWindowLong(WindowHandle, User32.WindowLongFlags.GWL_EXSTYLE, (nint)styleEx);
        User32.SetWindowPos(WindowHandle, Borderless ? HWND.HWND_TOPMOST : HWND.HWND_NOTOPMOST, rt.X, rt.Y, rt.Width, rt.Height, 0);
        AppWindow.IsShownInSwitchers = !Borderless;
    }


    private void MenuFlyoutItem_RandomMotion_Click(object sender, RoutedEventArgs e)
    {
        if (live2dPanel.LApp.Live2dManager.GetModelNum() > 0)
        {
            live2dPanel.LApp.Live2dManager.GetModel(0).StartMotion("表情组", Random.Shared.Next(3, 11));
        }
    }

    private void MenuFlyoutItem_Opacity_Click(object sender, RoutedEventArgs e)
    {
        FlyoutBase.ShowAttachedFlyout(Border_DragArea);
    }


    private void MenuFlyoutItem_CloseWindow_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void MenuFlyoutItem_Flip_Click(object sender, RoutedEventArgs e)
    {
        live2dPanel.FlipView();
    }
}


public partial class SwapChainOpacityToolTipValueConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is double d)
        {
            return d.ToString("F2");
        }
        return "-";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }

}
