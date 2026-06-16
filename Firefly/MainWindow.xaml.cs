using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Timers;
using Vanara.PInvoke;
using Windows.Graphics;
using WinRT.Interop;


namespace Firefly;

[ObservableObject]
public sealed partial class MainWindow : Window
{


    public string AppVersion => AppSetting.AppVersion;


    public nint WindowHandle => WindowNative.GetWindowHandle(this);

    public float UIScale => User32.GetDpiForWindow(WindowHandle) / 96f;


    private readonly ComCtl32.SUBCLASSPROC windowSubclassProc;



    public MainWindow()
    {
        InitializeComponent();
        InitializeMainWindow();
        windowSubclassProc = new(WindowSubclassProc);
        ComCtl32.SetWindowSubclass(WindowHandle, windowSubclassProc, 1001, IntPtr.Zero);
    }



    #region Window Method


    private void InitializeMainWindow()
    {
        AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
        AppWindow.TitleBar.IconShowOptions = IconShowOptions.ShowIconAndSystemMenu;
        AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
        AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        AppWindow.Closing += AppWindow_Closing;
        Live2dWindows.CollectionChanged += Live2dWindows_CollectionChanged;
        Content.KeyDown += Content_KeyDown;
        AppWindow.TitleBar.SetDragRectangles([new RectInt32(0, 0, 100000, (int)(48 * UIScale))]);
        var flag = User32.GetWindowLongPtr(WindowHandle, User32.WindowLongFlags.GWL_STYLE);
        flag &= ~(nint)User32.WindowStyles.WS_MAXIMIZEBOX;
        flag &= ~(nint)User32.WindowStyles.WS_SIZEBOX;
        User32.SetWindowLong(WindowHandle, User32.WindowLongFlags.GWL_STYLE, flag);
        CenterInScreen(1000, 540);
        SetIcon();
    }


    public void CenterInScreen(int? width = null, int? height = null)
    {
        DisplayArea display = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest);
        double scale = UIScale;
        int w = (int)((width * scale) ?? AppWindow.Size.Width);
        int h = (int)((height * scale) ?? AppWindow.Size.Height);
        int x = display.WorkArea.X + (display.WorkArea.Width - w) / 2;
        int y = display.WorkArea.Y + (display.WorkArea.Height - h) / 2;
        AppWindow.MoveAndResize(new RectInt32(x, y, w, h));
    }


    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        args.Cancel = true;
        Hide();
    }


    private void Content_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key is Windows.System.VirtualKey.Escape)
        {
            Hide();
        }
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


    public void Show()
    {
        AppWindow.Show(true);
        User32.SetForegroundWindow(WindowHandle);
    }


    public void Hide()
    {
        if (live2dPanel.LApp.Live2dManager.GetModelNum() > 0)
        {
            PlayPause();
        }
        AppWindow.Hide();
    }


    private IntPtr WindowSubclassProc(HWND hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, nuint uIdSubclass, IntPtr dwRefData)
    {
        if (uMsg == (uint)User32.WindowMessage.WM_SYSCOMMAND)
        {
            if (wParam == 0xF030)
            {
                // SC_MAXIMIZE
                // 防止双击标题栏使窗口最大化，WinAppSDK 某个版本的 Bug
                return IntPtr.Zero;
            }
        }
        return ComCtl32.DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }


    #endregion



    private async void Grid_Loaded(object sender, RoutedEventArgs e)
    {
        await Task.Delay(100);
        CreateCheckUpdateTimer();
        if (!AppSetting.Live2dWindowTeachingTipDismissed)
        {
            TeachingTip_NewLive2dWindow.IsOpen = true;
        }
        try
        {
            string file = Path.Combine(AppContext.BaseDirectory, "model", "FileReferences_Moc_0.model3.json");
            if (File.Exists(file))
            {
                live2dPanel.LoadModel(Path.Combine(AppContext.BaseDirectory, "model"), "FileReferences_Moc_0");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }



    #region Live2d Control


    private const string PauseIcon = "\xE769";

    private const string PlayIcon = "\xE768";


    [RelayCommand]
    private void PlayPause()
    {
        try
        {
            if (live2dPanel.LApp.Live2dManager.GetModelNum() > 0)
            {
                live2dPanel.RemoveAllModels();
                FontIcon_PlayPause.Glyph = PlayIcon;
            }
            else
            {
                live2dPanel.LoadModel(Path.Combine(AppContext.BaseDirectory, "model"), "FileReferences_Moc_0");
                FontIcon_PlayPause.Glyph = PauseIcon;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }


    [RelayCommand]
    private void StartRandomMotion()
    {
        try
        {
            if (live2dPanel.LApp.Live2dManager.GetModelNum() > 0)
            {
                live2dPanel.LApp.Live2dManager.GetModel(0).StartMotion("表情组", Random.Shared.Next(3, 11));
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }


    private bool _showHitArea;
    private DispatcherTimer? _hitAreaTimer;

    [RelayCommand]
    private void ToggleHitArea()
    {
        if (_hitAreaTimer is null)
        {
            _hitAreaTimer = new DispatcherTimer();
            _hitAreaTimer.Interval = TimeSpan.FromMilliseconds(16);
            _hitAreaTimer.Tick += UpdateHitAreas;
        }
        _showHitArea = !_showHitArea;
        if (_showHitArea)
        {
            _hitAreaTimer.Start();
        }
        else
        {
            _hitAreaTimer?.Stop();
            HitAreaCanvas.Children.Clear();
        }
    }

    private void UpdateHitAreas(object? sender, object e)
    {
        try
        {
            HitAreaCanvas.Children.Clear();
            if (!_showHitArea || live2dPanel.LApp.Live2dManager.GetModelNum() == 0) return;

            var model = live2dPanel.LApp.Live2dManager.GetModel(0);
            var hitAreas = model.GetHitAreaModelBounds();
            var modelMatrix = model.ModelMatrix;
            var projView = live2dPanel.LApp.Live2dManager.ProjectionViewMatrix;
            double panelWidth = live2dPanel.ActualWidth;
            double panelHeight = live2dPanel.ActualHeight;

            foreach (var (name, modelLeft, modelRight, modelTop, modelBottom) in hitAreas)
            {
                // Model坐标 → ModelMatrix → 投影坐标 → ProjectionViewMatrix → NDC → 屏幕
                float ndcLeft = projView.TransformX(modelMatrix.TransformX(modelLeft));
                float ndcRight = projView.TransformX(modelMatrix.TransformX(modelRight));
                float ndcTop = projView.TransformY(modelMatrix.TransformY(modelTop));
                float ndcBottom = projView.TransformY(modelMatrix.TransformY(modelBottom));

                float screenLeft = (ndcLeft + 1) * (float)panelWidth / 2;
                float screenRight = (ndcRight + 1) * (float)panelWidth / 2;
                float screenTop = (1 - ndcTop) * (float)panelHeight / 2;
                float screenBottom = (1 - ndcBottom) * (float)panelHeight / 2;

                var rect = new Microsoft.UI.Xaml.Shapes.Rectangle
                {
                    Width = Math.Abs(screenRight - screenLeft),
                    Height = Math.Abs(screenBottom - screenTop),
                    Stroke = new SolidColorBrush(Colors.Red),
                    StrokeThickness = 2,
                    IsHitTestVisible = false
                };

                Canvas.SetLeft(rect, Math.Min(screenLeft, screenRight));
                Canvas.SetTop(rect, Math.Min(screenTop, screenBottom));
                HitAreaCanvas.Children.Add(rect);

                var label = new TextBlock
                {
                    Text = name,
                    Foreground = new SolidColorBrush(Colors.Red),
                    FontSize = 12,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(label, Math.Min(screenLeft, screenRight));
                Canvas.SetTop(label, Math.Min(screenTop, screenBottom) - 16);
                HitAreaCanvas.Children.Add(label);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }


    #endregion




    #region Live2d Windows



    public ObservableCollection<Live2DWindowInfo> Live2dWindows { get; set => SetProperty(ref field, value); } = [];


    private int windowNameIndex;


    [RelayCommand]
    private void NewLive2dWindow()
    {
        try
        {
            var window = new Live2dWindow();
            string id = Guid.NewGuid().ToString();
            var info = new Live2DWindowInfo(id, (++windowNameIndex).ToString(), window);
            info.Closed += Live2DWindowInfo_Closed;
            Live2dWindows.Add(info);
            window.Activate();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }


    internal void RestoreLive2dWindow(Live2dWindowState state)
    {
        try
        {
            var window = new Live2dWindow();
            windowNameIndex++;
            window.AppWindow.MoveAndResize(new RectInt32(state.X, state.Y, state.Width, state.Height));
            if (state.Borderless)
            {
                window.ApplyBorderless(true);
            }
            if (state.Flipped)
            {
                window.FlipView();
            }
            if (state.Opacity < 1.0)
            {
                window.PanelOpacity = state.Opacity;
            }
            var info = new Live2DWindowInfo(state.Id, windowNameIndex.ToString(), window);
            info.Closed += Live2DWindowInfo_Closed;
            Live2dWindows.Add(info);
            window.Activate();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }


    private void Live2DWindowInfo_Closed(object? sender, object e)
    {
        if (sender is Live2DWindowInfo info)
        {
            info.Closed -= Live2DWindowInfo_Closed;
            Live2dWindowState.DeleteById(info.Id);
            Live2dWindows.Remove(info);
        }
    }


    private void Live2dWindows_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Live2dWindowState.SaveAll(Live2dWindows);
    }



    private void TeachingTip_NewLive2dWindow_Closed(TeachingTip sender, TeachingTipClosedEventArgs args)
    {
        AppSetting.Live2dWindowTeachingTipDismissed = true;
    }


    #endregion




    #region Release



    private Timer _checkUpdateTimer;


    private ReleaseInfo? _newRelease;


    public bool HasNewRelease { get; set => SetProperty(ref field, value); }



    private void CreateCheckUpdateTimer()
    {
#if !DEBUG
        _ = CheckUpdateAsync();
        _checkUpdateTimer = new Timer(TimeSpan.FromMinutes(60).TotalMilliseconds);
        _checkUpdateTimer.Elapsed += (_, _) => _ = CheckUpdateAsync();
        _checkUpdateTimer.Start();
#endif
    }



    private async Task CheckUpdateAsync()
    {
        try
        {
            var newInfo = await ReleaseInfo.GetLatestAsync();
            if (newInfo is not null)
            {
                Version oldVersion = Version.Parse(_newRelease?.Version ?? AppVersion);
                Version newVersion = Version.Parse(newInfo.Version);
                if (newVersion > oldVersion)
                {
                    _newRelease = newInfo;
                    DispatcherQueue.TryEnqueue(() => HasNewRelease = true);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }



    #endregion





    [RelayCommand]
    private async Task ShowInfoAsync()
    {
        await new InfoDialog() { XamlRoot = Content.XamlRoot, NewRelease = _newRelease }.ShowAsync();
    }

}
