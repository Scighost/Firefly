using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;

namespace Live2DCSharpSDK.WinUI.LApp;

public class TextureInfoD3D11 : TextureInfo
{

    private readonly ComPtr<ID3D11Texture2D> _texture;

    private readonly ComPtr<ID3D11ShaderResourceView> _resourceView;

    public ComPtr<ID3D11ShaderResourceView> ResourceView => _resourceView;

    public TextureInfoD3D11(ComPtr<ID3D11Texture2D> texture, ComPtr<ID3D11ShaderResourceView> resourceView)
    {
        _texture = texture;
        _resourceView = resourceView;
    }


    public override void Dispose()
    {
        _resourceView.Dispose();
        _texture.Dispose();
    }

}
