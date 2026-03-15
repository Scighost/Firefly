using Live2DCSharpSDK.Framework;
using Live2DCSharpSDK.WPF.LApp;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using Silk.NET.DXGI;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using SilkD3D11 = Silk.NET.Direct3D11;

namespace Live2DCSharpSDK.WPF;

/// <summary>
/// WPF Live2D 控件。
/// 渲染管线：D3D11 RenderTarget → DXGI 共享句柄 → D3D9Ex 纹理 → IDirect3DSurface9 → D3DImage
/// </summary>
public class Live2DImage : D3DImage, IDisposable
{
    // ── D3D11 ─────────────────────────────────────────────────────────
    private readonly SilkD3D11.D3D11 _d3d11Api;
    private ComPtr<ID3D11Device> _d3d11Device;
    private ComPtr<ID3D11DeviceContext> _d3d11Context;
    private ComPtr<ID3D11Texture2D> _renderTexture;
    private ComPtr<ID3D11RenderTargetView> _renderTargetView;

    // ── D3D9Ex bridge（D3DImage 只接受 IDirect3DSurface9）────────────
    private nint _d3d9Ex;       // IDirect3D9Ex*
    private nint _d3d9Device;   // IDirect3DDevice9Ex*
    private nint _d3d9Texture;  // IDirect3DTexture9*
    private nint _d3d9Surface;  // IDirect3DSurface9*

    private int _width;
    private int _height;
    private bool _modelLoaded;
    private long _lastTs;

    private readonly System.Timers.Timer _idleTimer;

    public LAppDelegateD3D11 LApp { get; private set; }

    public Live2DImage()
    {
        _d3d11Api = SilkD3D11.D3D11.GetApi(null);
        CreateD3D11Device();
        CreateD3D9ExDevice();

        if (!CubismFramework.IsStarted)
        {
            var allocator = new LAppAllocator();
            var option = new CubismOption
            {
                LogFunction = msg => Debug.WriteLine(msg),
                LoggingLevel = LogLevel.Debug,
            };
            CubismFramework.StartUp(allocator, option);
        }

        LApp = new LAppDelegateD3D11(_d3d11Device, _d3d11Context);

        _idleTimer = new System.Timers.Timer(Random.Shared.Next(10_000, 20_000));
        _idleTimer.Elapsed += (_, _) =>
        {
            if (_modelLoaded && LApp.Live2dManager.GetModelNum() > 0)
            {
                LApp.Live2dManager.GetModel(0).TryStartIdleMotion();
                _idleTimer.Interval = Random.Shared.Next(20_000, 40_000);
            }
        };
        _idleTimer.Start();

        CompositionTarget.Rendering += OnRendering;
        IsFrontBufferAvailableChanged += OnFrontBufferAvailableChanged;
    }

    private unsafe void OnRendering(object? sender, EventArgs e)
    {
        if (!_modelLoaded || _d3d9Surface == 0 || _renderTargetView.Handle is null)
            return;
        if (!IsFrontBufferAvailable)
            return;

        // D3D11 渲染必须在 Lock/Unlock 内部，WPF 合成器才能正确读到新帧
        Lock();
        try
        {
            var vp = new Viewport { Width = (uint)_width, Height = (uint)_height, MinDepth = 0, MaxDepth = 1 };
            _d3d11Context.RSSetViewports(1, ref vp);
            _d3d11Context.OMSetRenderTargets(1, _renderTargetView.GetAddressOf(), (ID3D11DepthStencilView*)null);

            Span<float> clear = [0, 0, 0, 0];
            _d3d11Context.ClearRenderTargetView(_renderTargetView, clear);

            long ts = Stopwatch.GetTimestamp();
            LApp.Run((ts - _lastTs) / (float)Stopwatch.Frequency);
            _lastTs = ts;

            _d3d11Context.Flush();

            AddDirtyRect(new Int32Rect(0, 0, _width, _height));
        }
        finally
        {
            Unlock();
        }
    }

    private void OnFrontBufferAvailableChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        // 设备丢失恢复后重建共享面
        if ((bool)e.NewValue && _width > 0 && _height > 0)
            RecreateSharedSurface();
    }

    public void SetSize(int width, int height)
    {
        if (width <= 0 || height <= 0 || (_width == width && _height == height))
            return;

        _width = width;
        _height = height;
        LApp.Width = width;
        LApp.Height = height;

        RecreateSharedSurface();
    }

    public void LoadModel(string folder, string name)
    {
        LApp.Live2dManager.LoadModel(folder, name);
        _modelLoaded = true;
        _lastTs = Stopwatch.GetTimestamp();
    }

    public void RemoveAllModels()
    {
        _modelLoaded = false;
        LApp.Live2dManager.ReleaseAllModel();
    }

    private FrameworkElement? _host;

    /// <summary>
    /// 绑定宿主元素（通常是承载此 D3DImage 的 &lt;Image&gt;）。
    /// 宿主尺寸变化时会自动调用 SetSize，无需在 XAML 中手动触发。
    /// </summary>
    public void Attach(FrameworkElement host)
    {
        if (_host != null)
        {
            _host.SizeChanged -= OnHostSizeChanged;
            _host.Loaded -= OnHostLoaded;
        }
        _host = host;
        _host.SizeChanged += OnHostSizeChanged;
        _host.Loaded += OnHostLoaded;

        // 若宿主已布局完成则立即更新尺寸
        if (host.ActualWidth > 0 && host.ActualHeight > 0)
            SetSize((int)host.ActualWidth, (int)host.ActualHeight);
    }

    private void OnHostLoaded(object sender, RoutedEventArgs e)
    {
        if (_host is { ActualWidth: > 0, ActualHeight: > 0 })
            SetSize((int)_host.ActualWidth, (int)_host.ActualHeight);
    }

    private void OnHostSizeChanged(object sender, SizeChangedEventArgs e)
    {
        SetSize((int)e.NewSize.Width, (int)e.NewSize.Height);
    }

    public void MouseDragged(float x, float y)
    {
        if (_modelLoaded)
            LApp.Live2dManager.OnDrag(x, y);
    }

    public void OnTap(float x, float y)
    {
        if (_modelLoaded)
        {
            LApp.Live2dManager.OnTap(x, y);
        }
    }

    // ── D3D11 设备创建 ────────────────────────────────────────────────

    private unsafe void CreateD3D11Device()
    {
        D3DFeatureLevel level = D3DFeatureLevel.Level111;
        D3DFeatureLevel featureLevel = 0;
        HResult hr = _d3d11Api.CreateDevice(
            null, D3DDriverType.Hardware, 0,
#if DEBUG
            (uint)(CreateDeviceFlag.BgraSupport | CreateDeviceFlag.Debug),
#else
            (uint)CreateDeviceFlag.BgraSupport,
#endif
            ref level, 1, SilkD3D11.D3D11.SdkVersion,
            _d3d11Device.GetAddressOf(), ref featureLevel, _d3d11Context.GetAddressOf());
        Marshal.ThrowExceptionForHR(hr);
    }

    // ── D3D9Ex 设备创建（仅用于桥接 D3DImage）────────────────────────

    private unsafe void CreateD3D9ExDevice()
    {
        int hr = NativeMethods.Direct3DCreate9Ex(NativeMethods.D3D_SDK_VERSION, out _d3d9Ex);
        Marshal.ThrowExceptionForHR(hr);

        var pp = new NativeMethods.D3DPRESENT_PARAMETERS
        {
            Windowed = 1,
            SwapEffect = 1,          // D3DSWAPEFFECT_DISCARD
            BackBufferFormat = 0,    // D3DFMT_UNKNOWN（窗口模式可用）
            BackBufferWidth = 1,
            BackBufferHeight = 1,
            PresentationInterval = 0x80000000U, // D3DPRESENT_INTERVAL_IMMEDIATE
        };

        hr = NativeMethods.IDirect3D9Ex_CreateDeviceEx(
            _d3d9Ex,
            0,                              // D3DADAPTER_DEFAULT
            1,                              // D3DDEVTYPE_HAL
            NativeMethods.GetDesktopWindow(),
            NativeMethods.D3DCREATE_HARDWARE_VERTEXPROCESSING
                | NativeMethods.D3DCREATE_MULTITHREADED
                | NativeMethods.D3DCREATE_FPU_PRESERVE,
            ref pp,
            null,                           // 窗口模式无需传 DisplayModeEx
            out _d3d9Device);
        Marshal.ThrowExceptionForHR(hr);
    }

    // ── 共享面重建：D3D11 纹理 ↔ D3D9Ex 纹理（通过 DXGI 共享句柄）───

    private unsafe void RecreateSharedSurface()
    {
        ReleaseD3D9Surface();

        if (_renderTargetView.Handle != null)
            _d3d11Context.OMSetRenderTargets(0, (ID3D11RenderTargetView**)null, (ID3D11DepthStencilView*)null);

        _renderTargetView.Dispose(); _renderTargetView = default;
        _renderTexture.Dispose(); _renderTexture = default;

        if (_width <= 0 || _height <= 0)
            return;

        // 1. 创建 D3D11 共享渲染目标纹理（BGRA 格式与 D3D9 A8R8G8B8 二进制兼容）
        var texDesc = new Texture2DDesc
        {
            Width = (uint)_width,
            Height = (uint)_height,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.FormatB8G8R8A8Unorm,
            SampleDesc = new SampleDesc(1, 0),
            Usage = Usage.Default,
            BindFlags = (uint)(BindFlag.RenderTarget | BindFlag.ShaderResource),
            MiscFlags = (uint)ResourceMiscFlag.Shared,
        };
        HResult hr = _d3d11Device.CreateTexture2D(ref texDesc, (SubresourceData*)null, _renderTexture.GetAddressOf());
        Marshal.ThrowExceptionForHR(hr);

        hr = _d3d11Device.CreateRenderTargetView(
            (ID3D11Resource*)_renderTexture.Handle, (RenderTargetViewDesc*)null,
            _renderTargetView.GetAddressOf());
        Marshal.ThrowExceptionForHR(hr);

        // 2. 获取 DXGI 共享句柄（legacy shared handle，D3D9Ex 需要此格式）
        using ComPtr<IDXGIResource> dxgiRes = _renderTexture.QueryInterface<IDXGIResource>();
        void* sharedHandle = null;
        hr = dxgiRes.GetSharedHandle(ref sharedHandle);
        Marshal.ThrowExceptionForHR(hr);

        // 3. 用共享句柄在 D3D9Ex 侧打开同一块纹理内存
        int hr9 = NativeMethods.IDirect3DDevice9Ex_CreateTexture(
            _d3d9Device,
            (uint)_width, (uint)_height, 1,
            NativeMethods.D3DUSAGE_RENDERTARGET,
            NativeMethods.D3DFMT_A8R8G8B8,
            NativeMethods.D3DPOOL_DEFAULT,
            out _d3d9Texture,
            &sharedHandle);  // HANDLE* pSharedHandle
        Marshal.ThrowExceptionForHR(hr9);

        // 4. 取第 0 级 surface
        hr9 = NativeMethods.IDirect3DTexture9_GetSurfaceLevel(_d3d9Texture, 0, out _d3d9Surface);
        Marshal.ThrowExceptionForHR(hr9);

        // 5. 绑定到 D3DImage
        Lock();
        SetBackBuffer(D3DResourceType.IDirect3DSurface9, _d3d9Surface);
        Unlock();
    }

    private void ReleaseD3D9Surface()
    {
        if (_d3d9Surface != 0) { NativeMethods.IUnknown_Release(_d3d9Surface); _d3d9Surface = 0; }
        if (_d3d9Texture != 0) { NativeMethods.IUnknown_Release(_d3d9Texture); _d3d9Texture = 0; }

        if (IsFrontBufferAvailable)
        {
            Lock();
            SetBackBuffer(D3DResourceType.IDirect3DSurface9, 0);
            Unlock();
        }
    }

    public void Dispose()
    {
        CompositionTarget.Rendering -= OnRendering;
        IsFrontBufferAvailableChanged -= OnFrontBufferAvailableChanged;
        _idleTimer.Stop();
        _idleTimer.Dispose();

        if (_host != null)
        {
            _host.SizeChanged -= OnHostSizeChanged;
            _host.Loaded -= OnHostLoaded;
            _host = null;
        }

        LApp?.Dispose();

        unsafe
        {
            if (_d3d11Context.Handle != null)
                _d3d11Context.OMSetRenderTargets(0, (ID3D11RenderTargetView**)null, (ID3D11DepthStencilView*)null);
        }

        _renderTargetView.Dispose();
        _renderTexture.Dispose();

        ReleaseD3D9Surface();
        if (_d3d9Device != 0) { NativeMethods.IUnknown_Release(_d3d9Device); _d3d9Device = 0; }
        if (_d3d9Ex != 0) { NativeMethods.IUnknown_Release(_d3d9Ex); _d3d9Ex = 0; }

        _d3d11Context.Dispose();
        _d3d11Device.Dispose();
        _d3d11Api.Dispose();
    }

    // ── D3D9Ex / COM 原生互操作 ───────────────────────────────────────

    private static unsafe class NativeMethods
    {
        public const uint D3D_SDK_VERSION = 32u;
        public const uint D3DUSAGE_RENDERTARGET = 0x00000001u;
        public const uint D3DFMT_A8R8G8B8 = 21u;
        public const uint D3DPOOL_DEFAULT = 0u;
        public const uint D3DCREATE_FPU_PRESERVE = 0x00000002u;
        public const uint D3DCREATE_MULTITHREADED = 0x00000004u;
        public const uint D3DCREATE_HARDWARE_VERTEXPROCESSING = 0x00000040u;

        [StructLayout(LayoutKind.Sequential)]
        public struct D3DPRESENT_PARAMETERS
        {
            public uint BackBufferWidth, BackBufferHeight, BackBufferFormat, BackBufferCount;
            public uint MultiSampleType, MultiSampleQuality, SwapEffect;
            public nint hDeviceWindow;
            public int Windowed, EnableAutoDepthStencil;
            public uint AutoDepthStencilFormat, Flags, FullScreen_RefreshRateInHz, PresentationInterval;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct D3DDISPLAYMODEEX
        {
            public uint Size, Width, Height, RefreshRate, Format, ScanLineOrdering;
        }

        [DllImport("d3d9.dll")]
        public static extern int Direct3DCreate9Ex(uint sdkVersion, out nint ppD3D);

        [DllImport("user32.dll")]
        public static extern nint GetDesktopWindow();

        // IUnknown::Release（vtable slot 2）
        public static uint IUnknown_Release(nint punk)
        {
            var fn = (delegate* unmanaged[Stdcall]<nint, uint>)(*(void***)punk)[2];
            return fn(punk);
        }

        // IDirect3D9Ex::CreateDeviceEx（vtable slot 20）
        public static int IDirect3D9Ex_CreateDeviceEx(
            nint self, uint adapter, uint devType, nint hFocus, uint flags,
            ref D3DPRESENT_PARAMETERS pp, D3DDISPLAYMODEEX* pMode, out nint ppDevice)
        {
            var fn = (delegate* unmanaged[Stdcall]<nint, uint, uint, nint, uint,
                          D3DPRESENT_PARAMETERS*, D3DDISPLAYMODEEX*, nint*, int>)
                     (*(void***)self)[20];
            fixed (D3DPRESENT_PARAMETERS* ppp = &pp)
            {
                nint dev = 0;
                int hr = fn(self, adapter, devType, hFocus, flags, ppp, pMode, &dev);
                ppDevice = dev;
                return hr;
            }
        }

        // IDirect3DDevice9::CreateTexture（vtable slot 23）
        public static int IDirect3DDevice9Ex_CreateTexture(
            nint self, uint w, uint h, uint levels, uint usage, uint fmt, uint pool,
            out nint ppTex, void* pSharedHandle)
        {
            var fn = (delegate* unmanaged[Stdcall]<nint, uint, uint, uint, uint, uint, uint, nint*, void*, int>)
                     (*(void***)self)[23];
            nint tex = 0;
            int hr = fn(self, w, h, levels, usage, fmt, pool, &tex, pSharedHandle);
            ppTex = tex;
            return hr;
        }

        // IDirect3DTexture9::GetSurfaceLevel（vtable slot 18）
        public static int IDirect3DTexture9_GetSurfaceLevel(nint self, uint level, out nint ppSurface)
        {
            var fn = (delegate* unmanaged[Stdcall]<nint, uint, nint*, int>)
                     (*(void***)self)[18];
            nint surf = 0;
            int hr = fn(self, level, &surf);
            ppSurface = surf;
            return hr;
        }
    }
}