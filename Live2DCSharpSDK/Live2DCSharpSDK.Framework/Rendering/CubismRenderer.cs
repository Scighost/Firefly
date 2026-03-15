using Live2DCSharpSDK.Framework.Math;
using Live2DCSharpSDK.Framework.Model;

namespace Live2DCSharpSDK.Framework.Rendering;

/// <summary>
/// 处理模型绘制的渲染器
/// 在子类中描述依赖环境的绘制指令
/// </summary>
public abstract class CubismRenderer
{
    /// <summary>
    /// 纹理各向异性过滤的参数
    /// </summary>
    public float Anisotropy { get; set; }
    /// <summary>
    /// 渲染目标模型
    /// </summary>
    public CubismModel Model { get; private set; }
    /// <summary>
    /// 已预乘 Alpha 时为 true
    /// </summary>
    public bool IsPremultipliedAlpha { get; set; }
    /// <summary>
    /// 背面剔除有效时为 true
    /// </summary>
    public bool IsCulling { get; set; }
    /// <summary>
    /// false 时统一绘制蒙板，true 时在每个部件绘制时重写蒙板
    /// </summary>
    public bool UseHighPrecisionMask { get; set; }

    /// <summary>
    /// 模型自身的颜色（RGBA）
    /// </summary>
    public CubismTextureColor ModelColor = new();

    /// <summary>
    /// MVP 矩阵
    /// </summary>
    private readonly CubismMatrix44 _mvpMatrix4x4 = new();

    /// <summary>
    /// 创建并获取渲染器实例
    /// </summary>
    public CubismRenderer(CubismModel model)
    {
        _mvpMatrix4x4.LoadIdentity();
        Model = model ?? throw new Exception("model is null");
    }

    /// <summary>
    /// 释放渲染器实例
    /// </summary>
    public abstract void Dispose();

    /// <summary>
    /// 绘制模型
    /// </summary>
    public void DrawModel()
    {
        /**
         * 请在 DoDrawModel 绘制前后调用以下函数。
         * - SaveProfile();
         * - RestoreProfile();
         * 这是通过保存和恢复渲染器的绘制设置，
         * 将状态恢复到模型绘制前的处理。
         */

        SaveProfile();

        DoDrawModel();

        RestoreProfile();
    }

    /// <summary>
    /// 设置 MVP 矩阵
    /// 数组会被复制，可在外部释放原始数组
    /// </summary>
    /// <param name="matrix4x4">MVP 矩阵</param>
    public void SetMvpMatrix(CubismMatrix44 matrix4x4)
    {
        _mvpMatrix4x4.SetMatrix(matrix4x4.Tr);
    }

    /// <summary>
    /// 获取 MVP 矩阵
    /// </summary>
    /// <returns>MVP 矩阵</returns>
    public CubismMatrix44 GetMvpMatrix()
    {
        return _mvpMatrix4x4;
    }

    /// <summary>
    /// 计算考虑不透明度后的模型颜色。
    /// </summary>
    /// <param name="opacity">不透明度</param>
    /// <returns>RGBA 颜色信息</returns>
    public CubismTextureColor GetModelColorWithOpacity(float opacity)
    {
        CubismTextureColor modelColorRGBA = new(ModelColor);
        modelColorRGBA.A *= opacity;
        if (IsPremultipliedAlpha)
        {
            modelColorRGBA.R *= modelColorRGBA.A;
            modelColorRGBA.G *= modelColorRGBA.A;
            modelColorRGBA.B *= modelColorRGBA.A;
        }
        return modelColorRGBA;
    }

    /// <summary>
    /// 设置模型颜色。
    /// 各颜色在 0.0f~1.0f 之间指定（1.0f 为标准状态）。
    /// </summary>
    /// <param name="red">红通道的倘</param>
    /// <param name="green">绿通道的倘</param>
    /// <param name="blue">蓝通道的倘</param>
    /// <param name="alpha">Alpha 通道的倘</param>
    public void SetModelColor(float red, float green, float blue, float alpha)
    {
        if (red < 0.0f) red = 0.0f;
        else if (red > 1.0f) red = 1.0f;

        if (green < 0.0f) green = 0.0f;
        else if (green > 1.0f) green = 1.0f;

        if (blue < 0.0f) blue = 0.0f;
        else if (blue > 1.0f) blue = 1.0f;

        if (alpha < 0.0f) alpha = 0.0f;
        else if (alpha > 1.0f) alpha = 1.0f;

        ModelColor.R = red;
        ModelColor.G = green;
        ModelColor.B = blue;
        ModelColor.A = alpha;
    }

    /// <summary>
    /// 模型绘制的实现
    /// </summary>
    protected abstract void DoDrawModel();

    /// <summary>
    /// 保存模型绘制前的渲染器状态
    /// </summary>
    protected abstract void SaveProfile();

    /// <summary>
    /// 恢复模型绘制前的渲染器状态
    /// </summary>
    protected abstract void RestoreProfile();
}
