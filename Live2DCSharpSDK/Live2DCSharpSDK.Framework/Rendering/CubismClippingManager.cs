using Live2DCSharpSDK.Framework.Math;
using Live2DCSharpSDK.Framework.Model;
using Live2DCSharpSDK.Framework.Type;
using System.Numerics;

namespace Live2DCSharpSDK.Framework.Rendering;

public abstract class CubismClippingManager
{
    public abstract RenderType RenderType { get; }
    public unsafe abstract CubismClippingContext CreateClippingContext(CubismClippingManager manager,
        CubismModel model, int* clippingDrawableIndices, int clipCount);

    /// <summary>
    /// 实验时单通道为 1，仅 RGB 时为 3，包含 Alpha 时为 4
    /// </summary>
    public const int ColorChannelCount = 4;
    /// <summary>
    /// 普通帧缓冲每张的蒙板最大数量
    /// </summary>
    public const int ClippingMaskMaxCountOnDefault = 36;
    /// <summary>
    /// 帧缓冲为 2 张及以上时，每张帧缓冲的蒙板最大数量
    /// </summary>
    public const int ClippingMaskMaxCountOnMultiRenderTexture = 32;

    /// <summary>
    /// 离屏表面的地址
    /// </summary>
    protected CubismOffscreenSurface CurrentMaskBuffer;
    /// <summary>
    /// 蒙板清除标志的数组
    /// </summary>
    protected List<bool> ClearedMaskBufferFlags = [];

    protected List<CubismTextureColor> ChannelColors = [];
    /// <summary>
    /// 蒙板用裁剪上下文列表
    /// </summary>
    protected List<CubismClippingContext> ClippingContextListForMask = [];
    /// <summary>
    /// 绘制用裁剪上下文列表
    /// </summary>
    public List<CubismClippingContext?> ClippingContextListForDraw { get; init; } = [];
    /// <summary>
    /// 裁剪蒙板缓冲大小（初始値：256）
    /// </summary>
    public Vector2 ClippingMaskBufferSize { get; private set; }
    /// <summary>
    /// 生成的渲染纹理数量
    /// </summary>
    public int RenderTextureCount { get; private set; }

    /// <summary>
    /// 蒙板计算用矩阵
    /// </summary>
    protected CubismMatrix44 TmpMatrix = new();
    /// <summary>
    /// 蒙板计算用矩阵
    /// </summary>
    protected CubismMatrix44 TmpMatrixForMask = new();
    /// <summary>
    /// 蒙板计算用矩阵
    /// </summary>
    protected CubismMatrix44 TmpMatrixForDraw = new();
    /// <summary>
    /// 蒙板布局计算用矩形
    /// </summary>
    protected RectF TmpBoundsOnModel = new();

    public CubismClippingManager()
    {
        ClippingMaskBufferSize = new(256, 256);

        ChannelColors.Add(new(1.0f, 0f, 0f, 0f));
        ChannelColors.Add(new(0f, 1.0f, 0f, 0f));
        ChannelColors.Add(new(0f, 0f, 1.0f, 0f));
        ChannelColors.Add(new(0f, 0f, 0f, 1.0f));
    }

    public void Dispose()
    {
        ClippingContextListForDraw.Clear();
        ClippingContextListForMask.Clear();
        ChannelColors.Clear();
        ClearedMaskBufferFlags.Clear();
    }

    /// <summary>
    /// 管理器初始化处理，注册使用裁剪蒙板的绘制对象
    /// </summary>
    /// <param name="model">模型实例</param>
    /// <param name="maskBufferCount">缓冲生成数量</param>
    public unsafe void Initialize(CubismModel model, int maskBufferCount)
    {
        RenderTextureCount = maskBufferCount;

        // 设置渲染纹理的清除标志
        for (int i = 0; i < RenderTextureCount; ++i)
        {
            ClearedMaskBufferFlags.Add(false);
        }

        //注册所有使用裁剪蒙板的绘制对象
        //裁剪蒙板通常限制在几个左右使用
        for (int i = 0; i < model.GetDrawableCount(); i++)
        {
            if (model.GetDrawableMaskCounts()[i] <= 0)
            {
                //未使用裁剪蒙板的绘制网格（大多数情况不使用）
                ClippingContextListForDraw.Add(null);
                continue;
            }

            // 检查是否与已有的 ClipContext 相同
            var cc = FindSameClip(model.GetDrawableMasks()[i], model.GetDrawableMaskCounts()[i]);
            if (cc == null)
            {
                // 若不存在相同的蒙板则生成
                cc = CreateClippingContext(this, model, model.GetDrawableMasks()[i], model.GetDrawableMaskCounts()[i]);
                ClippingContextListForMask.Add(cc);
            }

            cc.AddClippedDrawable(i);

            ClippingContextListForDraw.Add(cc);
        }
    }

    /// <summary>
    /// 确认是否已创建蒙板。
    /// 如已创建，则返回对应裁剪蒙板的实例。
    /// 未创建则返回 NULL
    /// </summary>
    /// <param name="drawableMasks">对绘制对象进行蒙板的绘制对象列表</param>
    /// <param name="drawableMaskCounts">对绘制对象进行蒙板的绘制对象数量</param>
    /// <returns>若存在对应裁剪蒙板则返回实例，否则返回 NULL。</returns>
    private unsafe CubismClippingContext? FindSameClip(int* drawableMasks, int drawableMaskCounts)
    {
        // 确认是否与已创建的 ClippingContext 一致
        for (int i = 0; i < ClippingContextListForMask.Count; i++)
        {
            var cc = ClippingContextListForMask[i];
            int count = cc.ClippingIdCount;
            if (count != drawableMaskCounts) continue; //数量不同则为不同对象
            int samecount = 0;

            // 确认是否持有相同 ID。由于数组数量相同，若匹配数量一致则视为持有相同内容。
            for (int j = 0; j < count; j++)
            {
                int clipId = cc.ClippingIdList[j];
                for (int k = 0; k < count; k++)
                {
                    if (drawableMasks[k] == clipId)
                    {
                        samecount++;
                        break;
                    }
                }
            }
            if (samecount == count)
            {
                return cc;
            }
        }
        return null; //未找到
    }

    /// <summary>
    /// 计算高精度蒙板处理用矩阵
    /// </summary>
    /// <param name="model">模型实例</param>
    /// <param name="isRightHanded">处理是否为右手坐标系</param>
    public void SetupMatrixForHighPrecision(CubismModel model, bool isRightHanded)
    {
        // 准备所有裁剪
        // 使用相同裁剪（多个情况合并为一个）时只设置一次
        int usingClipCount = 0;
        for (int clipIndex = 0; clipIndex < ClippingContextListForMask.Count; clipIndex++)
        {
            // 关于一个裁剪蒙板
            var cc = ClippingContextListForMask[clipIndex];

            // 计算包围所有使用此裁剪的绘制对象的矩形
            CalcClippedDrawTotalBounds(model, cc);

            if (cc.IsUsing)
            {
                usingClipCount++; //计为使用中
            }
        }

        if (usingClipCount <= 0)
        {
            return;
        }
        // 蒙板矩阵创建处理
        SetupLayoutBounds(0);

        // 若大小与渲染纹理数量不匹配则进行调整
        if (ClearedMaskBufferFlags.Count != RenderTextureCount)
        {
            ClearedMaskBufferFlags.Clear();

            for (int i = 0; i < RenderTextureCount; ++i)
            {
                ClearedMaskBufferFlags.Add(false);
            }
        }
        else
        {
            // 每帧开始时初始化蒙板的清除标志
            for (int i = 0; i < RenderTextureCount; ++i)
            {
                ClearedMaskBufferFlags[i] = false;
            }
        }

        // 实际生成蒙板
        // 决定所有蒙板的布局方式，并记录到 ClipContext 和 ClippedDrawContext 中
        for (int clipIndex = 0; clipIndex < ClippingContextListForMask.Count; clipIndex++)
        {
            // --- 实际绘制一个蒙板 ---
            var clipContext = ClippingContextListForMask[clipIndex];
            var allClippedDrawRect = clipContext.AllClippedDrawRect; //使用此蒙板的所有绘制对象在逻辑坐标上的包围矩形
            var layoutBoundsOnTex01 = clipContext.LayoutBounds; //将蒙板容纳于此区域中
            float MARGIN = 0.05f;
            float scaleX;
            float scaleY;
            float ppu = model.GetPixelsPerUnit();
            float maskPixelWidth = clipContext.Manager.ClippingMaskBufferSize.X;
            float maskPixelHeight = clipContext.Manager.ClippingMaskBufferSize.Y;
            float physicalMaskWidth = layoutBoundsOnTex01.Width * maskPixelWidth;
            float physicalMaskHeight = layoutBoundsOnTex01.Height * maskPixelHeight;

            TmpBoundsOnModel.SetRect(allClippedDrawRect);
            if (TmpBoundsOnModel.Width * ppu > physicalMaskWidth)
            {
                TmpBoundsOnModel.Expand(allClippedDrawRect.Width * MARGIN, 0.0f);
                scaleX = layoutBoundsOnTex01.Width / TmpBoundsOnModel.Width;
            }
            else
            {
                scaleX = ppu / physicalMaskWidth;
            }

            if (TmpBoundsOnModel.Height * ppu > physicalMaskHeight)
            {
                TmpBoundsOnModel.Expand(0.0f, allClippedDrawRect.Height * MARGIN);
                scaleY = layoutBoundsOnTex01.Height / TmpBoundsOnModel.Height;
            }
            else
            {
                scaleY = ppu / physicalMaskHeight;
            }


            // 计算生成蒙板时使用的矩阵
            CreateMatrixForMask(isRightHanded, layoutBoundsOnTex01, scaleX, scaleY);

            clipContext.MatrixForMask.SetMatrix(TmpMatrixForMask.Tr);
            clipContext.MatrixForDraw.SetMatrix(TmpMatrixForDraw.Tr);
        }
    }

    /// <summary>
    /// 创建蒙板制作/绘制用矩阵。
    /// </summary>
    /// <param name="isRightHanded">指定是否将坐标视为右手系</param>
    /// <param name="layoutBoundsOnTex01">容纳蒙板的区域</param>
    /// <param name="scaleX">绘制对象的缩放比例</param>
    /// <param name="scaleY">绘制对象的缩放比例</param>
    protected void CreateMatrixForMask(bool isRightHanded, RectF layoutBoundsOnTex01, float scaleX, float scaleY)
    {
        TmpMatrix.LoadIdentity();
        {
            // 将 Layout 0..1 转换为 -1..1
            TmpMatrix.TranslateRelative(-1.0f, -1.0f);
            TmpMatrix.ScaleRelative(2.0f, 2.0f);
        }
        {
            // view to Layout0..1
            TmpMatrix.TranslateRelative(layoutBoundsOnTex01.X, layoutBoundsOnTex01.Y); //new = [translate]
            TmpMatrix.ScaleRelative(scaleX, scaleY); //new = [translate][scale]
            TmpMatrix.TranslateRelative(-TmpBoundsOnModel.X, -TmpBoundsOnModel.Y); //new = [translate][scale][translate]
        }
        // tmpMatrixForMask 为计算结果
        TmpMatrixForMask.SetMatrix(TmpMatrix.Tr);

        TmpMatrix.LoadIdentity();
        {
            TmpMatrix.TranslateRelative(layoutBoundsOnTex01.X, layoutBoundsOnTex01.Y * (isRightHanded ? -1.0f : 1.0f)); //new = [translate]
            TmpMatrix.ScaleRelative(scaleX, scaleY * (isRightHanded ? -1.0f : 1.0f)); //new = [translate][scale]
            TmpMatrix.TranslateRelative(-TmpBoundsOnModel.X, -TmpBoundsOnModel.Y); //new = [translate][scale][translate]
        }

        TmpMatrixForDraw.SetMatrix(TmpMatrix.Tr);
    }

    /// <summary>
    /// 配置裁剪上下文的布局。
    /// 尽可能充分利用一张渲染纹理来布局蒙板。
    /// 蒙板组数量 ≤4 时，在 RGBA 各通道各放置 1 个；5~6 个时，以 2,2,1,1 分配到 RGBA。
    /// </summary>
    /// <param name="usingClipCount">要配置的裁剪上下文数量</param>
    protected void SetupLayoutBounds(int usingClipCount)
    {
        int useClippingMaskMaxCount = RenderTextureCount <= 1
        ? ClippingMaskMaxCountOnDefault
        : ClippingMaskMaxCountOnMultiRenderTexture * RenderTextureCount;

        if (usingClipCount <= 0 || usingClipCount > useClippingMaskMaxCount)
        {
            if (usingClipCount > useClippingMaskMaxCount)
            {
                // 输出蒙板数量限制警告
                int count = usingClipCount - useClippingMaskMaxCount;
                CubismLog.Error("[Live2D SDK]not supported mask count : %d\n[Details] render texture count: %d\n, mask count : %d"
                    , count, RenderTextureCount, usingClipCount);
            }

            // 此情况下每次清空一个蒙板目标再使用
            for (int index = 0; index < ClippingContextListForMask.Count; index++)
            {
                CubismClippingContext cc = ClippingContextListForMask[index];
                cc.LayoutChannelIndex = 0; // 每次都会清除，固定使用即可
                cc.LayoutBounds.X = 0.0f;
                cc.LayoutBounds.Y = 0.0f;
                cc.LayoutBounds.Width = 1.0f;
                cc.LayoutBounds.Height = 1.0f;
                cc.BufferIndex = 0;
            }
            return;
        }

        // 渲染纹理为 1 张时进行 9 分割（最多 36 张）
        int layoutCountMaxValue = RenderTextureCount <= 1 ? 9 : 8;

        // 尽可能充分利用一张 RenderTexture 来布局蒙板
        // 蒙板组数量 ≤4 时，在 RGBA 各通道各放置 1 个，5~6 个时以 2,2,1,1 分配
        int countPerSheetDiv = (usingClipCount + RenderTextureCount - 1) / RenderTextureCount; // 每张渲染纹理分配几张（向上取整）
        int reduceLayoutTextureCount = usingClipCount % RenderTextureCount; // 减少1 张布局的渲染纹理数量（此数量的渲染纹理为目标）

        // 按顺序使用 RGBA
        int divCount = countPerSheetDiv / ColorChannelCount; //每个通道配置的基本蒙板数量
        int modCount = countPerSheetDiv % ColorChannelCount; //余数，在此编号的通道之前各分配一个

        // 依次准备 RGBA 各通道 (0:R , 1:G , 2:B, 3:A, )
        int curClipIndex = 0; //按顺序设置

        for (int renderTextureIndex = 0; renderTextureIndex < RenderTextureCount; renderTextureIndex++)
        {
            for (int channelIndex = 0; channelIndex < ColorChannelCount; channelIndex++)
            {
                // 在此通道中布局的数量
                // NOTE: 布局数量 = 每通道基本蒙板数 + 若为放置余数的通道则追加 1 个
                int layoutCount = divCount + (channelIndex < modCount ? 1 : 0);

                // 决定需要减少 1 张布局时的目标通道
                // div 为 0 时调整为正常索引范围内
                int checkChannelIndex = modCount + (divCount < 1 ? -1 : 0);

                // 当前通道为目标通道，且存在需减少布局数量的渲染纹理时
                if (channelIndex == checkChannelIndex && reduceLayoutTextureCount > 0)
                {
                    // 若当前渲染纹理为目标渲染纹理，则将布局数量减 1
                    layoutCount -= !(renderTextureIndex < reduceLayoutTextureCount) ? 1 : 0;
                }

                // 决定分割方式
                if (layoutCount == 0)
                {
                    // 不做任何操作
                }
                else if (layoutCount == 1)
                {
                    //全部直接使用
                    var cc = ClippingContextListForMask[curClipIndex++];
                    cc.LayoutChannelIndex = channelIndex;
                    cc.LayoutBounds.X = 0.0f;
                    cc.LayoutBounds.Y = 0.0f;
                    cc.LayoutBounds.Width = 1.0f;
                    cc.LayoutBounds.Height = 1.0f;
                    cc.BufferIndex = renderTextureIndex;
                }
                else if (layoutCount == 2)
                {
                    for (int i = 0; i < layoutCount; i++)
                    {
                        int xpos = i % 2;

                        var cc = ClippingContextListForMask[curClipIndex++];
                        cc.LayoutChannelIndex = channelIndex;

                        cc.LayoutBounds.X = xpos * 0.5f;
                        cc.LayoutBounds.Y = 0.0f;
                        cc.LayoutBounds.Width = 0.5f;
                        cc.LayoutBounds.Height = 1.0f;
                        cc.BufferIndex = renderTextureIndex;
                        //将 UV 分解为 2 部分使用
                    }
                }
                else if (layoutCount <= 4)
                {
                    //分为 4 份使用
                    for (int i = 0; i < layoutCount; i++)
                    {
                        int xpos = i % 2;
                        int ypos = i / 2;

                        var cc = ClippingContextListForMask[curClipIndex++];
                        cc.LayoutChannelIndex = channelIndex;

                        cc.LayoutBounds.X = xpos * 0.5f;
                        cc.LayoutBounds.Y = ypos * 0.5f;
                        cc.LayoutBounds.Width = 0.5f;
                        cc.LayoutBounds.Height = 0.5f;
                        cc.BufferIndex = renderTextureIndex;
                    }
                }
                else if (layoutCount <= layoutCountMaxValue)
                {
                    //分为 9 份使用
                    for (int i = 0; i < layoutCount; i++)
                    {
                        int xpos = i % 3;
                        int ypos = i / 3;

                        var cc = ClippingContextListForMask[curClipIndex++];
                        cc.LayoutChannelIndex = channelIndex;

                        cc.LayoutBounds.X = xpos / 3.0f;
                        cc.LayoutBounds.Y = ypos / 3.0f;
                        cc.LayoutBounds.Width = 1.0f / 3.0f;
                        cc.LayoutBounds.Height = 1.0f / 3.0f;
                        cc.BufferIndex = renderTextureIndex;
                    }
                }
                // 超过蒙板限制数量时的处理
                else
                {
                    int count = usingClipCount - useClippingMaskMaxCount;

                    // 开发模式下停止运行
                    throw new Exception($"not supported mask count : {count}\n[Details] render texture count: {RenderTextureCount}\n, mask count : {usingClipCount}");
                }
            }
        }
    }

    /// <summary>
    /// 计算所有被蒙板裁剪的绘制对象的包围矩形（模型坐标系）
    /// </summary>
    /// <param name="model">模型实例</param>
    /// <param name="clippingContext">裁剪蒙板的上下文</param>
    protected unsafe void CalcClippedDrawTotalBounds(CubismModel model, CubismClippingContext clippingContext)
    {
        // 所有被裁剪蒙板覆盖的绘制对象的整体矩形
        float clippedDrawTotalMinX = float.MaxValue, clippedDrawTotalMinY = float.MaxValue;
        float clippedDrawTotalMaxX = float.MinValue, clippedDrawTotalMaxY = float.MinValue;

        // 判断该蒙板是否实际需要
        // 只要有一个使用此裁剪的“绘制对象”可用，就需要生成蒙板

        int clippedDrawCount = clippingContext.ClippedDrawableIndexList.Count;
        for (int clippedDrawableIndex = 0; clippedDrawableIndex < clippedDrawCount; clippedDrawableIndex++)
        {
            // 求使用蒙板的绘制对象的绘制矩形
            int drawableIndex = clippingContext.ClippedDrawableIndexList[clippedDrawableIndex];

            int drawableVertexCount = model.GetDrawableVertexCount(drawableIndex);
            var drawableVertexes = model.GetDrawableVertices(drawableIndex);

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            int loop = drawableVertexCount * CubismFramework.VertexStep;
            for (int pi = CubismFramework.VertexOffset; pi < loop; pi += CubismFramework.VertexStep)
            {
                float x = drawableVertexes[pi];
                float y = drawableVertexes[pi + 1];
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }

            //
            if (minX == float.MaxValue) continue; //未获取到有效点，跳过

            // 反映到整体矩形
            if (minX < clippedDrawTotalMinX) clippedDrawTotalMinX = minX;
            if (minY < clippedDrawTotalMinY) clippedDrawTotalMinY = minY;
            if (maxX > clippedDrawTotalMaxX) clippedDrawTotalMaxX = maxX;
            if (maxY > clippedDrawTotalMaxY) clippedDrawTotalMaxY = maxY;
        }
        if (clippedDrawTotalMinX == float.MaxValue)
        {
            clippingContext.AllClippedDrawRect.X = 0.0f;
            clippingContext.AllClippedDrawRect.Y = 0.0f;
            clippingContext.AllClippedDrawRect.Width = 0.0f;
            clippingContext.AllClippedDrawRect.Height = 0.0f;
            clippingContext.IsUsing = false;
        }
        else
        {
            clippingContext.IsUsing = true;
            float w = clippedDrawTotalMaxX - clippedDrawTotalMinX;
            float h = clippedDrawTotalMaxY - clippedDrawTotalMinY;
            clippingContext.AllClippedDrawRect.X = clippedDrawTotalMinX;
            clippingContext.AllClippedDrawRect.Y = clippedDrawTotalMinY;
            clippingContext.AllClippedDrawRect.Width = w;
            clippingContext.AllClippedDrawRect.Height = h;
        }
    }

    /// <summary>
    /// 获取颜色通道（RGBA）的标志
    /// </summary>
    /// <param name="channelNo">颜色通道（RGBA）的编号 (0:R , 1:G , 2:B, 3:A)</param>
    /// <returns></returns>
    public CubismTextureColor GetChannelFlagAsColor(int channelNo)
    {
        return ChannelColors[channelNo];
    }

    /// <summary>
    /// 设置裁剪蒙板缓冲的大小
    /// </summary>
    /// <param name="width">裁剪蒙板缓冲的宽度</param>
    /// <param name="height">裁剪蒙板缓冲的高度</param>
    public void SetClippingMaskBufferSize(float width, float height)
    {
        ClippingMaskBufferSize = new Vector2(width, height);
    }
}
