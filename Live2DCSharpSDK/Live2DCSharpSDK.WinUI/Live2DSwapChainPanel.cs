using Live2DCSharpSDK.Framework;
using Live2DCSharpSDK.WinUI.LApp;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using Windows.Foundation;

namespace Live2DCSharpSDK.WinUI;

public class Live2DSwapChainPanel : D3D11SwapChainPanel
{


    public LAppDelegateD3D11 LApp { get; private set; }

    private readonly System.Timers.Timer _timer;

    public Live2DSwapChainPanel() : base()
    {
        CreateDeviceResources();
        if (!CubismFramework.IsStarted)
        {
            var cubismAllocator = new LAppAllocator();
            var cubismOption = new CubismOption()
            {
                LogFunction = (message) => Debug.WriteLine(message),
#if DEBUG
                LoggingLevel = LogLevel.Debug,
#else
                LoggingLevel = LogLevel.Warning,
#endif
            };
            CubismFramework.StartUp(cubismAllocator, cubismOption);
        }
        LApp = new LAppDelegateD3D11(_d3d11Device, _d3d11Context);
        PointerPressed += Live2DSwapChainPanel_PointerPressed;
        PointerReleased += Live2DSwapChainPanel_PointerReleased;
        PointerMoved += Live2DSwapChainPanel_PointerMoved;
        _timer = new System.Timers.Timer(Random.Shared.Next(10_000, 20_000));
        _timer.Elapsed += _timer_Elapsed;
        _timer.Start();
    }



    private void _timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
    {
        if (_modelLoaded)
        {
            if (LApp.Live2dManager.GetModelNum() > 0)
            {
                LApp.Live2dManager.GetModel(0).TryStartIdleMotion();
                _timer.Interval = Random.Shared.Next(20_000, 40_000);
            }
        }
    }



    protected override void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        base.OnSizeChanged(sender, e);
        LApp.Width = (int)(ActualWidth * CompositionScaleX);
        LApp.Height = (int)(ActualHeight * CompositionScaleY);
    }



    protected override void OnCompositionScaleChanged(SwapChainPanel sender, object args)
    {
        base.OnCompositionScaleChanged(sender, args);
        LApp.Width = (int)(ActualWidth * CompositionScaleX);
        LApp.Height = (int)(ActualHeight * CompositionScaleY);
    }



    protected override void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _timer.Dispose();
        PointerPressed -= Live2DSwapChainPanel_PointerPressed;
        PointerReleased -= Live2DSwapChainPanel_PointerReleased;
        PointerMoved -= Live2DSwapChainPanel_PointerMoved;
        _modelLoaded = false;
        LApp.Dispose();
        LApp = null;
        base.OnUnloaded(sender, e);
    }



    bool _modelLoaded;


    public void LoadModel(string folder, string name)
    {
        LApp.Live2dManager.LoadModel(folder, name);
        _modelLoaded = true;
        lastTs = Stopwatch.GetTimestamp();
    }



    public void RemoveAllModels()
    {
        _modelLoaded = false;
        LApp.Live2dManager.ReleaseAllModel();
    }


    long lastTs;


    protected override void OnRendering(object sender, object e)
    {
        if (RenderPaused)
        {
            return;
        }
        base.OnRendering(sender, e);
        if (_modelLoaded)
        {
            long ts = Stopwatch.GetTimestamp();
            LApp.Run((ts - lastTs) / (float)Stopwatch.Frequency);
            lastTs = ts;
            Present();
        }
    }




    public void MouseDragged(float x, float y)
    {
        if (_modelLoaded)
        {
            LApp.Live2dManager.OnDrag(x, y);
        }
    }





    private bool _pointerPressed;

    private bool _pointerMoved;

    private Point _lastPointPosition;


    private void Live2DSwapChainPanel_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        if (point.Properties.IsLeftButtonPressed)
        {
            this.CapturePointer(e.Pointer);
            _pointerPressed = true;
            _pointerMoved = false;
            _lastPointPosition = e.GetCurrentPoint(this).Position;
        }
    }


    private void Live2DSwapChainPanel_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        this.ReleasePointerCapture(e.Pointer);
        if (_pointerPressed)
        {
            LApp.Live2dManager.OnDrag(0, 0);
            if (!_pointerMoved)
            {
                Point p = e.GetCurrentPoint(this).Position;
                double x = p.X * 2 / this.ActualWidth - 1;
                double y = 1 - p.Y * 2 / this.ActualHeight;
                LApp.Live2dManager.OnTap((float)x, (float)y);
            }
            _pointerPressed = false;
            _pointerMoved = false;
        }
    }


    private void Live2DSwapChainPanel_PointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (_pointerPressed && _modelLoaded)
        {
            Point p = e.GetCurrentPoint(this).Position;
            if (!_pointerMoved && Math.Abs(p.X - _lastPointPosition.X) + Math.Abs(p.Y - _lastPointPosition.Y) > 6)
            {
                _pointerMoved = true;
            }
        }
    }



    public void PauseRender()
    {
        RenderPaused = true;
    }


    public void ResumeRender()
    {
        RenderPaused = false;
    }



}