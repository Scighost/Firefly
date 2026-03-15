using Live2DCSharpSDK.Framework.Math;
using Live2DCSharpSDK.Framework.Type;

namespace Live2DCSharpSDK.Framework.Rendering;

public unsafe abstract class CubismClippingContext(CubismClippingManager manager, int* clippingDrawableIndices, int clipCount)
{
    /// <summary>
    /// 当前绘制状态下需要准备蒙板时为 true
    /// </summary>
    public bool IsUsing;
    /// <summary>
    /// 裁剪蒙板的 ID 列表
    /// </summary>
    public unsafe int* ClippingIdList = clippingDrawableIndices;
    /// <summary>
    /// 裁剪蒙板的数量
    /// </summary>
    public int ClippingIdCount = clipCount;
    /// <summary>
    /// 将此裁剪配置到 RGBA 的哪个通道 (0:R , 1:G , 2:B , 3:A)
    /// </summary>
    public int LayoutChannelIndex = 0;
    /// <summary>
    /// 将蒙板放在蒙板通道的哪个区域（视图坐标 -1..1，UV 转换到 0..1）
    /// </summary>
    public RectF LayoutBounds = new();
    /// <summary>
    /// 此裁剪中所有被裁剪绘制对象的包围矩形（每次更新）
    /// </summary>
    public RectF AllClippedDrawRect = new();
    /// <summary>
    /// 保存蒙板位置计算结果的矩阵
    /// </summary>
    public CubismMatrix44 MatrixForMask = new();
    /// <summary>
    /// 保存绘制对象位置计算结果的矩阵
    /// </summary>
    public CubismMatrix44 MatrixForDraw = new();
    /// <summary>
    /// 被此蒙板裁剪的绘制对象列表
    /// </summary>
    public List<int> ClippedDrawableIndexList = [];
    /// <summary>
    /// 此蒙板所分配的渲染纹理（帧缓冲）或颜色缓冲的索引
    /// </summary>
    public int BufferIndex;

    public CubismClippingManager Manager { get; } = manager;

    /// <summary>
    /// 添加被此蒙板裁剪的绘制对象
    /// </summary>
    /// <param name="drawableIndex">要添加到裁剪对象的绘制对象索引</param>
    public void AddClippedDrawable(int drawableIndex)
    {
        ClippedDrawableIndexList.Add(drawableIndex);
    }
}
