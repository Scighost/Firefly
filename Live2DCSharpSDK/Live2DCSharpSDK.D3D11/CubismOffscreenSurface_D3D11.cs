using Live2DCSharpSDK.Framework.Rendering;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using Silk.NET.DXGI;

namespace Live2DCSharpSDK.D3D11;

public unsafe class CubismOffscreenSurface_D3D11 : CubismOffscreenSurface, IDisposable
{
    private ComPtr<ID3D11Texture2D> _texture;
    private ComPtr<ID3D11ShaderResourceView> _textureView;
    private ComPtr<ID3D11RenderTargetView> _renderTargetView;
    private ComPtr<ID3D11Texture2D> _depthTexture;
    private ComPtr<ID3D11DepthStencilView> _depthView;

    private ComPtr<ID3D11RenderTargetView> _backupRender;
    private ComPtr<ID3D11DepthStencilView> _backupDepth;

    private uint _bufferWidth;
    private uint _bufferHeight;

    public uint BufferWidth => _bufferWidth;
    public uint BufferHeight => _bufferHeight;
    public ComPtr<ID3D11ShaderResourceView> TextureView => _textureView;

    public bool IsValid => _texture.Handle is not null;

    public void BeginDraw(ComPtr<ID3D11DeviceContext> renderContext)
    {
        if (_textureView.Handle is null || _renderTargetView.Handle is null || _depthView.Handle is null)
        {
            return;
        }

        if (_backupRender.Handle is not null) _backupRender.Release();
        if (_backupDepth.Handle is not null) _backupDepth.Release();
        _backupRender = default;
        _backupDepth = default;

        ComPtr<ID3D11RenderTargetView> backupRender = new();
        ComPtr<ID3D11DepthStencilView> backupDepth = new();
        renderContext.OMGetRenderTargets(1, backupRender.GetAddressOf(), backupDepth.GetAddressOf());
        _backupRender = backupRender;
        _backupDepth = backupDepth;

        // Unbind texture from shader resources to avoid hazard
        ID3D11ShaderResourceView** viewArray = stackalloc ID3D11ShaderResourceView*[2];
        viewArray[0] = null;
        viewArray[1] = null;
        renderContext.PSSetShaderResources(0, 2, viewArray);

        ComPtr<ID3D11RenderTargetView> rtv = _renderTargetView;
        renderContext.OMSetRenderTargets(1, rtv.GetAddressOf(), _depthView);
    }

    public void EndDraw(ComPtr<ID3D11DeviceContext> renderContext)
    {
        if (_textureView.Handle is null || _renderTargetView.Handle is null || _depthView.Handle is null)
        {
            return;
        }

        ComPtr<ID3D11RenderTargetView> backupRender = _backupRender;
        renderContext.OMSetRenderTargets(1, backupRender.GetAddressOf(), _backupDepth);

        if (_backupDepth.Handle is not null)
        {
            _backupDepth.Release();
            _backupDepth = default;
        }
        if (_backupRender.Handle is not null)
        {
            _backupRender.Release();
            _backupRender = default;
        }
    }

    public void Clear(ComPtr<ID3D11DeviceContext> renderContext, float r, float g, float b, float a)
    {
        float* clearColor = stackalloc float[4] { r, g, b, a };
        renderContext.ClearRenderTargetView(_renderTargetView, clearColor);
        renderContext.ClearDepthStencilView(_depthView, (uint)ClearFlag.Depth, 1.0f, 0);
    }

    public bool CreateOffscreenSurface(ComPtr<ID3D11Device> device, uint displayBufferWidth, uint displayBufferHeight)
    {
        DestroyOffscreenSurface();

        _bufferWidth = displayBufferWidth;
        _bufferHeight = displayBufferHeight;

        // Texture
        var textureDesc = new Texture2DDesc
        {
            Usage = Usage.Default,
            Format = Format.FormatR8G8B8A8Unorm,
            BindFlags = (uint)(BindFlag.RenderTarget | BindFlag.ShaderResource),
            Width = displayBufferWidth,
            Height = displayBufferHeight,
            CPUAccessFlags = 0,
            MipLevels = 1,
            ArraySize = 1,
            SampleDesc = new SampleDesc(1, 0)
        };

        ComPtr<ID3D11Texture2D> texture = new();
        if (device.CreateTexture2D(ref textureDesc, null, texture.GetAddressOf()) < 0) return false;
        _texture = texture;

        // RenderTargetView
        var renderTargetViewDesc = new RenderTargetViewDesc
        {
            Format = Format.FormatR8G8B8A8Unorm,
            ViewDimension = RtvDimension.Texture2D
        };
        renderTargetViewDesc.Texture2D.MipSlice = 0;

        ComPtr<ID3D11RenderTargetView> rtv = new();
        if (device.Get().CreateRenderTargetView((ID3D11Resource*)texture.Handle, ref renderTargetViewDesc, rtv.GetAddressOf()) < 0) return false;
        _renderTargetView = rtv;

        // ShaderResourceView
        var shaderResourceViewDesc = new ShaderResourceViewDesc
        {
            Format = Format.FormatR8G8B8A8Unorm,
            ViewDimension = D3DSrvDimension.D3DSrvDimensionTexture2D
        };
        shaderResourceViewDesc.Texture2D.MostDetailedMip = 0;
        shaderResourceViewDesc.Texture2D.MipLevels = 1;

        ComPtr<ID3D11ShaderResourceView> srv = new();
        if (device.Get().CreateShaderResourceView((ID3D11Resource*)texture.Handle, ref shaderResourceViewDesc, srv.GetAddressOf()) < 0) return false;
        _textureView = srv;

        // Depth Texture
        var depthTextureDesc = new Texture2DDesc
        {
            Usage = Usage.Default,
            Format = Format.FormatD24UnormS8Uint,
            BindFlags = (uint)BindFlag.DepthStencil,
            Width = displayBufferWidth,
            Height = displayBufferHeight,
            CPUAccessFlags = 0,
            MipLevels = 1,
            ArraySize = 1,
            SampleDesc = new SampleDesc(1, 0)
        };

        ComPtr<ID3D11Texture2D> depthTexture = new();
        if (device.CreateTexture2D(ref depthTextureDesc, null, depthTexture.GetAddressOf()) < 0) return false;
        _depthTexture = depthTexture;

        // DepthStencilView
        var depthStencilViewDesc = new DepthStencilViewDesc
        {
            Format = Format.FormatD24UnormS8Uint,
            ViewDimension = DsvDimension.Texture2D
        };
        depthStencilViewDesc.Texture2D.MipSlice = 0;

        ComPtr<ID3D11DepthStencilView> dsv = new();
        if (device.Get().CreateDepthStencilView((ID3D11Resource*)depthTexture.Handle, ref depthStencilViewDesc, dsv.GetAddressOf()) < 0) return false;
        _depthView = dsv;

        return true;
    }

    public void DestroyOffscreenSurface()
    {
        if (_depthView.Handle is not null) { _depthView.Release(); _depthView = default; }
        if (_depthTexture.Handle is not null) { _depthTexture.Release(); _depthTexture = default; }
        if (_renderTargetView.Handle is not null) { _renderTargetView.Release(); _renderTargetView = default; }
        if (_textureView.Handle is not null) { _textureView.Release(); _textureView = default; }
        if (_texture.Handle is not null) { _texture.Release(); _texture = default; }
    }

    public void Dispose()
    {
        DestroyOffscreenSurface();
    }
}
