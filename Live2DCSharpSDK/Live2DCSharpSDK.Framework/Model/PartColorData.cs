using Live2DCSharpSDK.Framework.Rendering;

namespace Live2DCSharpSDK.Framework.Model;

/// <summary>
/// 以 RGBA 格式管理纹理颜色的结构体
/// </summary>
public record PartColorData
{
    public bool IsOverwritten { get; set; }
    public CubismTextureColor Color = new();
}
