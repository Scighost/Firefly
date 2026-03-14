using Live2DCSharpSDK.Framework.Rendering;

namespace Live2DCSharpSDK.D3D11;

public unsafe class CubismClippingContext_D3D11 : CubismClippingContext
{
    public CubismClippingContext_D3D11(CubismClippingManager manager, int* clippingDrawableIndices, int clipCount)
        : base(manager, clippingDrawableIndices, clipCount)
    {
    }
}
