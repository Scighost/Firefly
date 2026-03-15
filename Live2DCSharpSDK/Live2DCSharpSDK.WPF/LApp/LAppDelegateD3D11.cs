using Live2DCSharpSDK.D3D11;
using Live2DCSharpSDK.Framework.Model;
using Live2DCSharpSDK.Framework.Rendering;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using Silk.NET.DXGI;
using System.Runtime.InteropServices;

namespace Live2DCSharpSDK.WPF.LApp;

public class LAppDelegateD3D11 : LAppDelegate
{



    private readonly ComPtr<ID3D11Device> _device;

    private readonly ComPtr<ID3D11DeviceContext> _context;


    public int Width { get; set; }

    public int Height { get; set; }



    public LAppDelegateD3D11(ComPtr<ID3D11Device> device, ComPtr<ID3D11DeviceContext> context)
    {
        _device = device;
        _context = context;
        View = new LAppViewD3D11(this);
        InitApp();
    }



    public override CubismRenderer CreateRenderer(CubismModel model)
    {
        return new CubismRenderer_D3D11(_device, _context, model);
    }

    public override void RebindTexture(LAppModel model, int index, TextureInfo info)
    {
        if (info is TextureInfoD3D11 d3dInfo)
        {
            (model.Renderer as CubismRenderer_D3D11)?.BindTexture(index, d3dInfo.ResourceView);
        }
    }


    public override unsafe TextureInfo CreateTexture(LAppModel model, int index, int width, int height, nint data)
    {
        var texDesc = new Texture2DDesc
        {
            Width = (uint)width,
            Height = (uint)height,
            MipLevels = 0,  // 0 = auto-generate all mip levels
            ArraySize = 1,
            Format = Format.FormatR8G8B8A8Unorm,
            SampleDesc = new SampleDesc(1, 0),
            Usage = Usage.Default,
            BindFlags = (uint)(BindFlag.ShaderResource | BindFlag.RenderTarget),
            CPUAccessFlags = 0,
            MiscFlags = (uint)ResourceMiscFlag.GenerateMips
        };
        ComPtr<ID3D11Texture2D> texture = new();
        HResult hr = _device.CreateTexture2D(ref texDesc, (SubresourceData*)null, texture.GetAddressOf());
        Marshal.ThrowExceptionForHR(hr);
        ComPtr<ID3D11ShaderResourceView> resourceView = new();
        hr = _device.CreateShaderResourceView((ID3D11Resource*)texture.Handle, (ShaderResourceViewDesc*)null, resourceView.GetAddressOf());
        Marshal.ThrowExceptionForHR(hr);
        // Upload mip0 then generate remaining levels
        _context.UpdateSubresource((ID3D11Resource*)texture.Handle, 0, (Box*)null, (void*)data, (uint)(width * 4), 0);
        _context.GenerateMips(resourceView);

        (model.Renderer as CubismRenderer_D3D11)?.BindTexture(index, resourceView);
        return new TextureInfoD3D11(texture, resourceView) { Index = index };
    }

    public override void GetWindowSize(out int width, out int height)
    {
        width = Width;
        height = Height;
    }


    public override bool RunPre()
    {
        return false;
    }

    public override void OnUpdatePre()
    {

    }

    public override void RunPost()
    {

    }


}
