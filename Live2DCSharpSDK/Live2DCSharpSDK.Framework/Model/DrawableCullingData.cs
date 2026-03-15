namespace Live2DCSharpSDK.Framework.Model;

/// <summary>
/// 用于管理纹理剪裁（Culling）设置的结构体
/// </summary>
public record DrawableCullingData
{
    public bool IsOverwritten { get; set; }
    public bool IsCulling { get; set; }
}
