using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.Windows.Storage.Pickers;
using Starward.Helpers;
using System;
using System.IO;
using Vanara.PInvoke;
using Windows.Graphics;


namespace App1;

public sealed partial class MainWindow : Window
{

    private nint WindowHandle => (nint)AppWindow.Id.Value;

    private float DpiScale => User32.GetDpiForWindow(WindowHandle) / 96f;

    public bool Borderless { get; set; }


    private ComCtl32.SUBCLASSPROC SUBCLASSPROC;

    private DispatcherQueueTimer _timer;

    private System.Timers.Timer _gcTimer;


    public MainWindow()
    {
        InitializeComponent();
        InitializeWindow();
        CreateTimer();
        string file = Path.Combine(AppContext.BaseDirectory, "model", "FileReferences_Moc_0.model3.json");
        if (File.Exists(file))
        {
            live2d.LoadModel(Path.Combine(AppContext.BaseDirectory, "model"), "FileReferences_Moc_0");
        }
    }



    private void InitializeWindow()
    {
        this.SizeChanged += MainWindow_SizeChanged;
        SystemBackdrop = new TransparentBackdrop();
        AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
        AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
        AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        var displayInfo = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest);
        int width = (int)(616 * DpiScale);
        int height = (int)(448 * DpiScale);
        int x = displayInfo.WorkArea.X + (displayInfo.WorkArea.Width - width) / 2;
        int y = displayInfo.WorkArea.Y + (displayInfo.WorkArea.Height - height) / 2;
        AppWindow.MoveAndResize(new RectInt32(x, y, width, height));
        SUBCLASSPROC = new ComCtl32.SUBCLASSPROC(SubclassProc);
        ComCtl32.SetWindowSubclass(WindowHandle, SUBCLASSPROC, 1, IntPtr.Zero);
    }


    private void CreateTimer()
    {
        _timer = DispatcherQueue.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(33);
        _timer.Tick += DispatcherQueueTimer_Tick;
        _timer.Start();
        _gcTimer = new(60_000);
        _gcTimer.Elapsed += (_, _) => GC.Collect();
        _gcTimer.Start();
    }




    private IntPtr SubclassProc(HWND hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, nuint uIdSubclass, IntPtr dwRefData)
    {
        if (Borderless && uMsg == (uint)User32.WindowMessage.WM_NCHITTEST)
        {
            return (IntPtr)User32.HitTestValues.HTTRANSPARENT;
        }
        return ComCtl32.DefSubclassProc(hWnd, uMsg, wParam, lParam);
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

        if (Borderless && live2d.LApp.Live2dManager.GetModelNum() > 0 && !live2d.LApp.Live2dManager.HitAnyDrawable(x, y))
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
                live2d.MouseDragged(Math.Clamp(x, -0.6f, 0.6f), Math.Clamp(y, -0.6f, 0.6f));
            }
        }
        else
        {
            live2d.MouseDragged(0, 0);
            _lastCursorPos = pt;
        }
    }


    private void MainWindow_SizeChanged(object sender, WindowSizeChangedEventArgs args)
    {
        int l = (int)(48 * DpiScale);
        int width = (int)(args.Size.Width * DpiScale - l);
        AppWindow.TitleBar.SetDragRectangles([new RectInt32(l, 0, width, l)]);
    }


    private async void MenuFlyoutItem_LoadModel_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker(AppWindow.Id);
        picker.FileTypeFilter.Add(".json");
        var result = await picker.PickSingleFileAsync();
        if (File.Exists(result?.Path) && result.Path.EndsWith(".model3.json"))
        {
            var dir = Path.GetDirectoryName(result.Path);
            string name = Path.GetFileName(result.Path).Replace(".model3.json", "");
            live2d.RemoveAllModels();
            live2d.LoadModel(dir, name);
        }
    }


    private void MenuFlyoutItem_BorderlessWindow_Click(object sender, RoutedEventArgs e)
    {
        int l = (int)(8 * DpiScale);
        User32.GetWindowRect(WindowHandle, out var rt);
        User32.WindowStyles style = (User32.WindowStyles)User32.GetWindowLong(WindowHandle, User32.WindowLongFlags.GWL_STYLE);
        if (Borderless)
        {
            Borderless = false;
            AppWindow.IsShownInSwitchers = true;
            Border_DragArea.Visibility = Visibility.Visible;
            style |= User32.WindowStyles.WS_OVERLAPPEDWINDOW;
            rt = new RECT(rt.X - l, rt.Y, rt.Right + l, rt.Bottom + l);
        }
        else
        {
            Borderless = true;
            AppWindow.IsShownInSwitchers = false;
            Border_DragArea.Visibility = Visibility.Collapsed;
            style &= ~User32.WindowStyles.WS_OVERLAPPEDWINDOW;
            rt = new RECT(rt.X + l, rt.Y, rt.Right - l, rt.Bottom - l);
        }
        User32.SetWindowLong(WindowHandle, User32.WindowLongFlags.GWL_STYLE, (nint)style);
        User32.SetWindowPos(WindowHandle, Borderless ? HWND.HWND_TOPMOST : HWND.HWND_NOTOPMOST, rt.X, rt.Y, rt.Width, rt.Height, 0);
    }



    private void MenuFlyoutItem_CloseWindow_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }


    private void Grid_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        Button_Menu.Opacity = 1;
    }

    private void Grid_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        Button_Menu.Opacity = 0;
    }

}
