namespace Live2DCSharpSDK.Framework.Rendering;

/// <summary>
/// 用 RGBA 处理纹理颜色的结构体
/// </summary>
public struct CubismTextureColor
{
    /// <summary>
    /// 红通道
    /// </summary>
    public float R;
    /// <summary>
    /// 绿通道
    /// </summary>
    public float G;
    /// <summary>
    /// 蓝通道
    /// </summary>
    public float B;
    /// <summary>
    /// Alpha 通道
    /// </summary>
    public float A;

    public CubismTextureColor()
    {
        R = 1.0f;
        G = 1.0f;
        B = 1.0f;
        A = 1.0f;
    }

    public CubismTextureColor(CubismTextureColor old)
    {
        R = old.R;
        G = old.G;
        B = old.B;
        A = old.A;
        Check();
    }

    public CubismTextureColor(float r, float g, float b, float a)
    {
        R = r;
        G = g;
        B = b;
        A = a;
        Check();
    }

    private void Check()
    {
        R = R > 1.0f ? 1f : R;
        G = G > 1.0f ? 1f : G;
        B = B > 1.0f ? 1f : B;
        A = A > 1.0f ? 1f : A;
    }
}