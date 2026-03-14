using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using Silk.NET.DXGI;
using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using WinRT;
using SilkD3D11 = Silk.NET.Direct3D11;

namespace Live2DCSharpSDK.WinUI;

public partial class D3D11SwapChainPanel : SwapChainPanel
{


    private SilkD3D11.D3D11 _d3d11Api;

    protected ComPtr<ID3D11Device> _d3d11Device;

    protected ComPtr<ID3D11DeviceContext> _d3d11Context;

    protected ComPtr<IDXGISwapChain1> _swapChain;

    protected ComPtr<ID3D11Texture2D> _texture2D;

    protected ComPtr<ID3D11RenderTargetView> _renderTargetView;



    public D3D11SwapChainPanel()
    {
#pragma warning disable CS0618 // 类型或成员已过时
        _d3d11Api = SilkD3D11.D3D11.GetApi(DXSwapchainProvider.Win32);
#pragma warning restore CS0618 // 类型或成员已过时
        SizeChanged += OnSizeChanged;
        Unloaded += OnUnloaded;
        CompositionScaleChanged += OnCompositionScaleChanged;
        CompositionTarget.Rendering += OnRendering;
    }



    protected virtual unsafe void OnUnloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        SizeChanged -= OnSizeChanged;
        Unloaded -= OnUnloaded;
        CompositionScaleChanged -= OnCompositionScaleChanged;
        CompositionTarget.Rendering -= OnRendering;

        if (_d3d11Context.Handle != null)
        {
            _d3d11Context.OMSetRenderTargets(0, (ID3D11RenderTargetView**)null, (ID3D11DepthStencilView*)null);
            _d3d11Context.ClearState();
            _d3d11Context.Flush();
        }
        _renderTargetView.Dispose();
        _texture2D.Dispose();
        _swapChain.Dispose();
        _d3d11Context.Dispose();
        _d3d11Device.Dispose();
    }



    protected virtual unsafe void OnRendering(object sender, object e)
    {
        if (_d3d11Context.Handle is null || _renderTargetView.Handle is null)
        {
            return;
        }

        uint width = (uint)(ActualWidth * CompositionScaleX);
        uint height = (uint)(ActualHeight * CompositionScaleY);
        var vp = new Viewport { Width = width, Height = height, MinDepth = 0, MaxDepth = 1 };
        _d3d11Context.RSSetViewports(1, ref vp);
        _d3d11Context.OMSetRenderTargets(1, _renderTargetView.GetAddressOf(), (ID3D11DepthStencilView*)null);

        Span<float> clear = [0, 0, 0, 0];
        _d3d11Context.ClearRenderTargetView(_renderTargetView, clear);
    }


    protected virtual void OnCompositionScaleChanged(SwapChainPanel sender, object args)
    {
        CreateDeviceResources();
    }


    protected virtual void OnSizeChanged(object sender, Microsoft.UI.Xaml.SizeChangedEventArgs e)
    {
        CreateDeviceResources();
    }



    protected unsafe void CreateDeviceResources()
    {
        uint width = (uint)(ActualWidth * CompositionScaleX);
        uint height = (uint)(ActualHeight * CompositionScaleY);

        if (_d3d11Device.Handle is null || _d3d11Context.Handle is null)
        {
            D3DFeatureLevel level = D3DFeatureLevel.Level111;
            D3DFeatureLevel fetureLevel = 0;
            HResult hr = _d3d11Api.CreateDevice(null,
                                               D3DDriverType.Hardware,
                                               0,
                                               (uint)(CreateDeviceFlag.BgraSupport | CreateDeviceFlag.Debug),
                                               ref level,
                                               1,
                                               SilkD3D11.D3D11.SdkVersion,
                                               _d3d11Device.GetAddressOf(),
                                               ref fetureLevel,
                                               _d3d11Context.GetAddressOf());
            Marshal.ThrowExceptionForHR(hr);

            var sd = new SwapChainDesc1
            {
                BufferCount = 2,
                Width = width == 0 ? 1 : width,
                Height = height == 0 ? 1 : height,
                Format = Format.FormatB8G8R8A8Unorm,
                BufferUsage = DXGI.UsageRenderTargetOutput,
                SwapEffect = SwapEffect.FlipSequential,
                AlphaMode = AlphaMode.Premultiplied,
                SampleDesc = new SampleDesc(1, 0),
            };

            ComPtr<IDXGIDevice> dxgiDevice = _d3d11Device.QueryInterface<IDXGIDevice>();
            ComPtr<IDXGIAdapter> adapter = new();
            hr = dxgiDevice.GetAdapter(ref adapter);
            Marshal.ThrowExceptionForHR(hr);
            hr = adapter.GetParent<IDXGIFactory2>().CreateSwapChainForComposition((IUnknown*)_d3d11Device.Handle, &sd, (IDXGIOutput*)null, _swapChain.GetAddressOf());
            Marshal.ThrowExceptionForHR(hr);
            hr = this.As<ISwapChainPanelNative>().SetSwapChain(_swapChain.Handle);
            Marshal.ThrowExceptionForHR(hr);
            adapter.Dispose();
            dxgiDevice.Dispose();

            _texture2D = _swapChain.GetBuffer<ID3D11Texture2D>(0);
            _d3d11Device.CreateRenderTargetView((ID3D11Resource*)_texture2D.Handle, (RenderTargetViewDesc*)null, _renderTargetView.GetAddressOf());

            ApplySwapChainScale();
        }
        else
        {
            _d3d11Context.OMSetRenderTargets(0, (ID3D11RenderTargetView**)null, (ID3D11DepthStencilView*)null);
            _d3d11Context.ClearState();
            _d3d11Context.Flush();

            _renderTargetView.Dispose();
            _texture2D.Dispose();
            _swapChain.ResizeBuffers(2, width, height, Format.FormatB8G8R8A8Unorm, 0);

            _texture2D = _swapChain.GetBuffer<ID3D11Texture2D>(0);
            _d3d11Device.CreateRenderTargetView((ID3D11Resource*)_texture2D.Handle, (RenderTargetViewDesc*)null, _renderTargetView.GetAddressOf());

            ApplySwapChainScale();
        }
    }


    public unsafe void Present()
    {
        HResult hr = _swapChain.Present(1, 0);

        if (hr == unchecked((int)0x887A0005) || hr == unchecked((int)0x887A0007))
        {
            //OnDeviceLost();
        }
        else if (hr < 0)
        {
            Marshal.ThrowExceptionForHR(_d3d11Device.GetDeviceRemovedReason());
        }
    }



    private unsafe void ApplySwapChainScale()
    {
        if (_swapChain.Handle is null)
        {
            return;
        }
        using ComPtr<IDXGISwapChain2> ptr = _swapChain.QueryInterface<IDXGISwapChain2>();
        if (ptr.Handle is not null)
        {
            Matrix3X2F scale = new Matrix3X2F { DXGI11 = 1f / CompositionScaleX, DXGI22 = 1f / CompositionScaleY };
            ptr.SetMatrixTransform(ref scale);
        }
    }


    [GeneratedComInterface]
    [Guid("63aad0b8-7c24-40ff-85a8-640d944cc325")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public partial interface ISwapChainPanelNative
    {
        [PreserveSig]
        public unsafe int SetSwapChain(IDXGISwapChain1* swapChain);
    }


}
