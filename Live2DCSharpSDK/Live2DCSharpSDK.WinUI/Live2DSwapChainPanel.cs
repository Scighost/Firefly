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


    private readonly LAppDelegateD3D11 _lApp;



    public Live2DSwapChainPanel() : base()
    {
        CreateDeviceResources();
        if (!CubismFramework.IsStarted)
        {
            var cubismAllocator = new LAppAllocator();
            var cubismOption = new CubismOption()
            {
                LogFunction = (message) => Debug.WriteLine(message),
                LoggingLevel = LogLevel.Debug,
            };
            CubismFramework.StartUp(cubismAllocator, cubismOption);
        }
        _lApp = new LAppDelegateD3D11(_d3d11Device, _d3d11Context);
        this.PointerPressed += Live2DSwapChainPanel_PointerPressed;
        this.PointerReleased += Live2DSwapChainPanel_PointerReleased;
        this.PointerMoved += Live2DSwapChainPanel_PointerMoved;
    }



    protected override void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        base.OnSizeChanged(sender, e);
        _lApp.Width = (int)(ActualWidth * CompositionScaleX);
        _lApp.Height = (int)(ActualHeight * CompositionScaleY);
    }



    protected override void OnCompositionScaleChanged(SwapChainPanel sender, object args)
    {
        base.OnCompositionScaleChanged(sender, args);
        _lApp.Width = (int)(ActualWidth * CompositionScaleX);
        _lApp.Height = (int)(ActualHeight * CompositionScaleY);
    }




    bool _modelLoaded;


    public void LoadModel(string folder, string name)
    {
        _lApp.Live2dManager.LoadModel(folder, name);
        _modelLoaded = true;
        lastTs = Stopwatch.GetTimestamp();
    }



    public void RemoveAllModels()
    {
        _modelLoaded = false;
        _lApp.Live2dManager.ReleaseAllModel();
    }


    long lastTs;


    protected override void OnRendering(object sender, object e)
    {
        base.OnRendering(sender, e);
        if (_modelLoaded)
        {
            long ts = Stopwatch.GetTimestamp();
            _lApp.Run((ts - lastTs) / (float)Stopwatch.Frequency);
            lastTs = ts;
            Present();
        }
    }



    private bool _pointerPressed;

    private bool _pointerMoved;

    private Point _lastPointPosition;


    private void Live2DSwapChainPanel_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        this.CapturePointer(e.Pointer);
        _pointerPressed = true;
        _pointerMoved = false;
        _lastPointPosition = e.GetCurrentPoint(this).Position;
    }


    private void Live2DSwapChainPanel_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        this.ReleasePointerCapture(e.Pointer);
        _lApp.Live2dManager.OnDrag(0, 0);
        if (!_pointerMoved)
        {
            Point p = e.GetCurrentPoint(this).Position;
            double x = p.X * 2 / this.ActualWidth - 1;
            double y = 1 - p.Y * 2 / this.ActualHeight;
            _lApp.Live2dManager.OnTap((float)x, (float)y);
        }
        _pointerPressed = false;
        _pointerMoved = false;
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
            else if (_pointerMoved)
            {
                double x = p.X * 2 / this.ActualWidth - 1;
                double y = 1 - p.Y * 2 / this.ActualHeight;
                _lApp.Live2dManager.OnDrag((float)x, (float)y);
            }
        }
    }




}