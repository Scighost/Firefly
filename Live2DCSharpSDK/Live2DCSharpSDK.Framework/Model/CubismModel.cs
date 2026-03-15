using System.Numerics;
using Live2DCSharpSDK.Framework.Core;
using Live2DCSharpSDK.Framework.Rendering;

namespace Live2DCSharpSDK.Framework.Model;

public class CubismModel : IDisposable
{
    /// <summary>
    /// 模型
    /// </summary>
    public IntPtr Model { get; }

    public readonly List<string> ParameterIds = [];
    public readonly List<string> PartIds = [];
    public readonly List<string> DrawableIds = [];

    /// <summary>
    /// 不存在部件的不透明度列表
    /// </summary>
    private readonly Dictionary<int, float> _notExistPartOpacities = [];
    /// <summary>
    /// 不存在部件 ID 的列表
    /// </summary>
    private readonly Dictionary<string, int> _notExistPartId = [];
    /// <summary>
    /// 不存在参数值的列表
    /// </summary>
    private readonly Dictionary<int, float> _notExistParameterValues = [];
    /// <summary>
    /// 不存在参数 ID 的列表
    /// </summary>
    private readonly Dictionary<string, int> _notExistParameterId = [];
    /// <summary>
    /// 已保存的参数
    /// </summary>
    private readonly List<float> _savedParameters = [];
    /// <summary>
    /// 参数值列表
    /// </summary>
    private readonly unsafe float* _parameterValues;
    /// <summary>
    /// 参数最大值列表
    /// </summary>
    private readonly unsafe float* _parameterMaximumValues;
    /// <summary>
    /// 参数最小值列表
    /// </summary>
    private readonly unsafe float* _parameterMinimumValues;
    /// <summary>
    /// 部件不透明度列表
    /// </summary>
    private readonly unsafe float* _partOpacities;
    /// <summary>
    /// 模型不透明度
    /// </summary>
    private float _modelOpacity;

    /// <summary>
    /// Drawable 的屏幕色数组
    /// </summary>
    private readonly List<DrawableColorData> _userScreenColors = [];
    /// <summary>
    /// Drawable 的乘算色数组
    /// </summary>
    private readonly List<DrawableColorData> _userMultiplyColors = [];
    /// <summary>
    /// 剔除（Culling）设置数组
    /// </summary>
    private readonly List<DrawableCullingData> _userCullings = [];
    /// <summary>
    /// 部件的屏幕色数组
    /// </summary>
    private readonly List<PartColorData> _userPartScreenColors = [];
    /// <summary>
    /// 部件的乘算色数组
    /// </summary>
    private readonly List<PartColorData> _userPartMultiplyColors = [];
    /// <summary>
    /// 部件的子 Drawable 索引数组
    /// </summary>
    private readonly List<int>[] _partChildDrawables;
    /// <summary>
    /// 是否覆盖所有乘算色？
    /// </summary>
    private bool _isOverwrittenModelMultiplyColors;
    /// <summary>
    /// 是否覆盖所有屏幕色？
    /// </summary>
    private bool _isOverwrittenModelScreenColors;
    /// <summary>
    /// 是否覆盖模型的所有剔除设置？
    /// </summary>
    private bool _isOverwrittenCullings;

    public unsafe CubismModel(IntPtr model)
    {
        Model = model;
        _modelOpacity = 1.0f;

        _parameterValues = CubismCore.GetParameterValues(Model);
        _partOpacities = CubismCore.GetPartOpacities(Model);
        _parameterMaximumValues = CubismCore.GetParameterMaximumValues(Model);
        _parameterMinimumValues = CubismCore.GetParameterMinimumValues(Model);

        {
            var parameterIds = CubismCore.GetParameterIds(Model);
            var parameterCount = CubismCore.GetParameterCount(Model);

            for (int i = 0; i < parameterCount; ++i)
            {
                var str = new string(parameterIds[i]);
                ParameterIds.Add(CubismFramework.CubismIdManager.GetId(str));
            }
        }

        int partCount = CubismCore.GetPartCount(Model);
        var partIds = CubismCore.GetPartIds(Model);

        _partChildDrawables = new List<int>[partCount];
        for (int i = 0; i < partCount; ++i)
        {
            var str = new string(partIds[i]);
            PartIds.Add(CubismFramework.CubismIdManager.GetId(str));
            _partChildDrawables[i] = [];
        }

        var drawableIds = CubismCore.GetDrawableIds(Model);
        var drawableCount = CubismCore.GetDrawableCount(Model);

        // 剔除设置
        var userCulling = new DrawableCullingData()
        {
            IsOverwritten = false,
            IsCulling = false
        };

        // 乘算色
        var multiplyColor = new CubismTextureColor();

        // 屏幕色
        var screenColor = new CubismTextureColor(0, 0, 0, 1.0f);

        // 部件
        for (int i = 0; i < partCount; ++i)
        {
            _userPartMultiplyColors.Add(new()
            {
                IsOverwritten = false,
                Color = multiplyColor // 乘算色
            });
            _userPartScreenColors.Add(new()
            {
                IsOverwritten = false,
                Color = screenColor // 屏幕色
            });
        }

        // Drawables
        for (int i = 0; i < drawableCount; ++i)
        {
            var str = new string(drawableIds[i]);
            DrawableIds.Add(CubismFramework.CubismIdManager.GetId(str));
            _userMultiplyColors.Add(new()
            {
                IsOverwritten = false,
                Color = multiplyColor // 乘算色
            });
            _userScreenColors.Add(new()
            {
                IsOverwritten = false,
                Color = screenColor   // 屏幕色
            });
            _userCullings.Add(userCulling);

            var parentIndex = CubismCore.GetDrawableParentPartIndices(Model)[i];
            if (parentIndex >= 0)
            {
                _partChildDrawables[parentIndex].Add(i);
            }
        }
    }

    public void Dispose()
    {
        CubismFramework.DeallocateAligned(Model);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 设置部件覆盖颜色的函数
    /// </summary>
    public void SetPartColor(int partIndex, float r, float g, float b, float a,
       List<PartColorData> partColors, List<DrawableColorData> drawableColors)
    {
        partColors[partIndex].Color.R = r;
        partColors[partIndex].Color.G = g;
        partColors[partIndex].Color.B = b;
        partColors[partIndex].Color.A = a;

        if (partColors[partIndex].IsOverwritten)
        {
            for (int i = 0; i < _partChildDrawables[partIndex].Count; i++)
            {
                int drawableIndex = _partChildDrawables[partIndex][i];
                drawableColors[drawableIndex].Color.R = r;
                drawableColors[drawableIndex].Color.G = g;
                drawableColors[drawableIndex].Color.B = b;
                drawableColors[drawableIndex].Color.A = a;
            }
        }
    }

    /// <summary>
    /// 设置部件覆盖标志的函数
    /// </summary>
    public void SetOverwriteColorForPartColors(int partIndex, bool value,
        List<PartColorData> partColors, List<DrawableColorData> drawableColors)
    {
        partColors[partIndex].IsOverwritten = value;

        for (int i = 0; i < _partChildDrawables[partIndex].Count; i++)
        {
            int drawableIndex = _partChildDrawables[partIndex][i];
            drawableColors[drawableIndex].IsOverwritten = value;
            if (value)
            {
                drawableColors[drawableIndex].Color.R = partColors[partIndex].Color.R;
                drawableColors[drawableIndex].Color.G = partColors[partIndex].Color.G;
                drawableColors[drawableIndex].Color.B = partColors[partIndex].Color.B;
                drawableColors[drawableIndex].Color.A = partColors[partIndex].Color.A;
            }
        }
    }

    /// <summary>
    /// 更新模型的参数。
    /// </summary>
    public void Update()
    {
        // Update model.
        CubismCore.UpdateModel(Model);
        // Reset dynamic drawable flags.
        CubismCore.ResetDrawableDynamicFlags(Model);
    }

    /// <summary>
    /// 以像素为单位获取画布宽度。
    /// </summary>
    /// <returns>画布宽度（像素）</returns>
    public float GetCanvasWidthPixel()
    {
        if (Model == IntPtr.Zero)
        {
            return 0.0f;
        }

        CubismCore.ReadCanvasInfo(Model, out var tmpSizeInPixels, out _, out _);

        return tmpSizeInPixels.X;
    }

    /// <summary>
    /// 以像素为单位获取画布高度。
    /// </summary>
    /// <returns>画布高度（像素）</returns>
    public float GetCanvasHeightPixel()
    {
        if (new IntPtr(Model) == IntPtr.Zero)
        {
            return 0.0f;
        }

        CubismCore.ReadCanvasInfo(Model, out var tmpSizeInPixels, out _, out _);

        return tmpSizeInPixels.Y;
    }

    /// <summary>
    /// 获取每单位像素数（PixelsPerUnit）。
    /// </summary>
    /// <returns>每单位像素数（PixelsPerUnit）</returns>
    public float GetPixelsPerUnit()
    {
        if (new IntPtr(Model) == IntPtr.Zero)
        {
            return 0.0f;
        }

        CubismCore.ReadCanvasInfo(Model, out _, out _, out var tmpPixelsPerUnit);

        return tmpPixelsPerUnit;
    }

    /// <summary>
    /// 以单位为单位获取画布宽度。
    /// </summary>
    /// <returns>画布宽度（单位）</returns>
    public float GetCanvasWidth()
    {
        CubismCore.ReadCanvasInfo(Model, out var tmpSizeInPixels, out _, out var tmpPixelsPerUnit);

        return tmpSizeInPixels.X / tmpPixelsPerUnit;
    }

    /// <summary>
    /// 以单位为单位获取画布高度。
    /// </summary>
    /// <returns>画布高度（单位）</returns>
    public float GetCanvasHeight()
    {
        CubismCore.ReadCanvasInfo(Model, out var tmpSizeInPixels, out _, out var tmpPixelsPerUnit);

        return tmpSizeInPixels.Y / tmpPixelsPerUnit;
    }

    /// <summary>
    /// 获取部件的索引。
    /// </summary>
    /// <param name="partId">部件的ID</param>
    /// <returns>部件的索引</returns>
    public int GetPartIndex(string partId)
    {
        int partIndex = PartIds.IndexOf(partId);
        if (partIndex != -1)
        {
            return partIndex;
        }

        int partCount = CubismCore.GetPartCount(Model);

        // 如果模型中不存在该部件，则在不存在部件 ID 列表中查找并返回其索引
        if (_notExistPartId.TryGetValue(partId, out var item))
        {
            return item;
        }

        // 若不存在于不存在部件 ID 列表，则新增一项
        partIndex = partCount + _notExistPartId.Count;

        _notExistPartId.TryAdd(partId, partIndex);
        _notExistPartOpacities.Add(partIndex, 0);

        return partIndex;
    }

    /// <summary>
    /// 获取部件的 ID。
    /// </summary>
    /// <param name="partIndex">部件索引</param>
    /// <returns>部件的 ID</returns>
    public unsafe string GetPartId(int partIndex)
    {
        if (0 <= partIndex && partIndex < PartIds.Count)
        {
            throw new IndexOutOfRangeException("Out of PartIds size");
        }
        return PartIds[partIndex];
    }

    /// <summary>
    /// 获取部件数量。
    /// </summary>
    /// <returns>部件数量</returns>
    public int GetPartCount()
    {
        return CubismCore.GetPartCount(Model);
    }

    /// <summary>
    /// 设置部件的不透明度。
    /// </summary>
    /// <param name="partId">部件的 ID</param>
    /// <param name="opacity">不透明度</param>
    public void SetPartOpacity(string partId, float opacity)
    {
        // 为了性能有获取 PartIndex 的机制，但从外部设置时调用频率较低，所以不用
        int index = GetPartIndex(partId);

        if (index < 0)
        {
            return; // 部件不存在，跳过
        }

        SetPartOpacity(index, opacity);
    }

    /// <summary>
    /// 设置部件的不透明度。
    /// </summary>
    /// <param name="partIndex">部件索引</param>
    /// <param name="opacity">部件不透明度</param>
    public unsafe void SetPartOpacity(int partIndex, float opacity)
    {
        if (_notExistPartOpacities.ContainsKey(partIndex))
        {
            _notExistPartOpacities[partIndex] = opacity;
            return;
        }

        // 索引范围检查
        if (0 > partIndex || partIndex >= GetPartCount())
        {
            throw new ArgumentException($"partIndex out of range");
        }

        _partOpacities[partIndex] = opacity;
    }

    /// <summary>
    /// 获取部件的不透明度。
    /// </summary>
    /// <param name="partId">部件 ID</param>
    /// <returns>部件不透明度</returns>
    public float GetPartOpacity(string partId)
    {
        // 为了性能有获取 PartIndex 的机制，但从外部设置时调用频率较低，所以不需要
        int index = GetPartIndex(partId);

        if (index < 0)
        {
            return 0; // 部件不存在，跳过
        }

        return GetPartOpacity(index);
    }

    /// <summary>
    /// 获取部件的不透明度。
    /// </summary>
    /// <param name="partIndex">部件索引</param>
    /// <returns>部件不透明度</returns>
    public unsafe float GetPartOpacity(int partIndex)
    {
        if (_notExistPartOpacities.TryGetValue(partIndex, out float value))
        {
            // 若为模型中不存在的部件 ID，则从不存在部件列表返回不透明度
            return value;
        }

        // 索引范围检查
        if (0 > partIndex || partIndex >= GetPartCount())
        {
            throw new ArgumentException($"partIndex out of range");
        }

        return _partOpacities[partIndex];
    }

    /// <summary>
    /// 获取参数的索引。
    /// </summary>
    /// <param name="parameterId">参数 ID</param>
    /// <returns>参数索引</returns>
    public int GetParameterIndex(string parameterId)
    {
        int parameterIndex = ParameterIds.IndexOf(parameterId);
        if (parameterIndex != -1)
        {
            return parameterIndex;
        }

        // 如果模型中不存在该参数，则在不存在参数 ID 列表中查找并返回其索引
        if (_notExistParameterId.TryGetValue(parameterId, out var data))
        {
            return data;
        }

        // 若不存在于不存在参数 ID 列表，则新增一项
        parameterIndex = CubismCore.GetParameterCount(Model) + _notExistParameterId.Count;

        _notExistParameterId.TryAdd(parameterId, parameterIndex);
        _notExistParameterValues.Add(parameterIndex, 0);

        return parameterIndex;
    }

    /// <summary>
    /// 获取参数个数。
    /// </summary>
    /// <returns>参数个数</returns>
    public int GetParameterCount()
    {
        return CubismCore.GetParameterCount(Model);
    }

    /// <summary>
    /// 获取参数类型。
    /// </summary>
    /// <param name="parameterIndex">参数索引</param>
    /// <returns>csmParameterType_Normal -> 普通参数
    /// csmParameterType_BlendShape -> BlendShape（混合形状）参数</returns>
    public unsafe int GetParameterType(int parameterIndex)
    {
        return CubismCore.GetParameterTypes(Model)[parameterIndex];
    }

    /// <summary>
    /// 获取参数的最大值。
    /// </summary>
    /// <param name="parameterIndex">参数索引</param>
    /// <returns>参数最大值</returns>
    public unsafe float GetParameterMaximumValue(int parameterIndex)
    {
        return CubismCore.GetParameterMaximumValues(Model)[parameterIndex];
    }

    /// <summary>
    /// 获取参数的最小值。
    /// </summary>
    /// <param name="parameterIndex">参数索引</param>
    /// <returns>参数最小值</returns>
    public unsafe float GetParameterMinimumValue(int parameterIndex)
    {
        return CubismCore.GetParameterMinimumValues(Model)[parameterIndex];
    }

    /// <summary>
    /// 获取参数的默认值。
    /// </summary>
    /// <param name="parameterIndex">参数索引</param>
    /// <returns>参数默认值</returns>
    public unsafe float GetParameterDefaultValue(int parameterIndex)
    {
        return CubismCore.GetParameterDefaultValues(Model)[parameterIndex];
    }

    /// <summary>
    /// 获取参数的当前值。
    /// </summary>
    /// <param name="parameterId">参数 ID</param>
    /// <returns>参数值</returns>
    public float GetParameterValue(string parameterId)
    {
        // 为了性能有获取 ParameterIndex 的机制，但从外部设置时调用频率较低，所以不用
        int parameterIndex = GetParameterIndex(parameterId);
        return GetParameterValue(parameterIndex);
    }

    /// <summary>
    /// 获取参数的当前值。
    /// </summary>
    /// <param name="parameterIndex">参数索引</param>
    /// <returns>参数值</returns>
    public unsafe float GetParameterValue(int parameterIndex)
    {
        if (_notExistParameterValues.TryGetValue(parameterIndex, out var item))
        {
            return item;
        }

        // 索引范围检查
        if (0 > parameterIndex || parameterIndex >= GetParameterCount())
        {
            throw new ArgumentException($"parameterIndex out of range");
        }

        return _parameterValues[parameterIndex];
    }

    /// <summary>
    /// 设置参数的值。
    /// </summary>
    /// <param name="parameterId">参数 ID</param>
    /// <param name="value">参数值</param>
    /// <param name="weight">权重</param>
    public void SetParameterValue(string parameterId, float value, float weight = 1.0f)
    {
        int index = GetParameterIndex(parameterId);
        SetParameterValue(index, value, weight);
    }

    /// <summary>
    /// 设置参数的值。
    /// </summary>
    /// <param name="parameterIndex">参数索引</param>
    /// <param name="value">参数值</param>
    /// <param name="weight">权重</param>
    public unsafe void SetParameterValue(int parameterIndex, float value, float weight = 1.0f)
    {
        if (_notExistParameterValues.TryGetValue(parameterIndex, out float value1))
        {
            _notExistParameterValues[parameterIndex] = weight == 1
                ? value : (value1 * (1 - weight)) + (value * weight);
            return;
        }

        // 索引范围检查
        if (0 > parameterIndex || parameterIndex >= GetParameterCount())
        {
            throw new ArgumentException($"parameterIndex out of range");
        }

        if (CubismCore.GetParameterMaximumValues(Model)[parameterIndex] < value)
        {
            value = CubismCore.GetParameterMaximumValues(Model)[parameterIndex];
        }
        if (CubismCore.GetParameterMinimumValues(Model)[parameterIndex] > value)
        {
            value = CubismCore.GetParameterMinimumValues(Model)[parameterIndex];
        }

        _parameterValues[parameterIndex] = (weight == 1)
                                          ? value
                                          : _parameterValues[parameterIndex] = (_parameterValues[parameterIndex] * (1 - weight)) + (value * weight);
    }

    /// <summary>
    /// 对参数值进行加法操作。
    /// </summary>
    /// <param name="parameterId">参数 ID</param>
    /// <param name="value">要加的值</param>
    /// <param name="weight">权重</param>
    public void AddParameterValue(string parameterId, float value, float weight = 1.0f)
    {
        int index = GetParameterIndex(parameterId);
        AddParameterValue(index, value, weight);
    }

    /// <summary>
    /// 对参数值进行加法操作。
    /// </summary>
    /// <param name="parameterIndex">参数索引</param>
    /// <param name="value">要加的值</param>
    /// <param name="weight">权重</param>
    public void AddParameterValue(int parameterIndex, float value, float weight = 1.0f)
    {
        if (parameterIndex == -1)
            return;
        SetParameterValue(parameterIndex, (GetParameterValue(parameterIndex) + (value * weight)));
    }

    /// <summary>
    /// 对参数值进行乘法操作。
    /// </summary>
    /// <param name="parameterId">参数 ID</param>
    /// <param name="value">乘数</param>
    /// <param name="weight">权重</param>
    public void MultiplyParameterValue(string parameterId, float value, float weight = 1.0f)
    {
        int index = GetParameterIndex(parameterId);
        MultiplyParameterValue(index, value, weight);
    }

    /// <summary>
    /// 对参数值进行乘法操作。
    /// </summary>
    /// <param name="parameterIndex">参数索引</param>
    /// <param name="value">乘数</param>
    /// <param name="weight">权重</param>
    public void MultiplyParameterValue(int parameterIndex, float value, float weight = 1.0f)
    {
        if (parameterIndex == -1)
            return;
        SetParameterValue(parameterIndex, GetParameterValue(parameterIndex) * (1.0f + (value - 1.0f) * weight));
    }

    /// <summary>
    /// 获取 Drawable 的索引。
    /// </summary>
    /// <param name="drawableId">Drawable 的 ID</param>
    /// <returns>Drawable 的索引</returns>
    public int GetDrawableIndex(string drawableId)
    {
        return DrawableIds.IndexOf(drawableId);
    }

    /// <summary>
    /// 获取 Drawable 的数量。
    /// </summary>
    /// <returns>Drawable 的数量</returns>
    public int GetDrawableCount()
    {
        return CubismCore.GetDrawableCount(Model);
    }

    /// <summary>
    /// 获取 Drawable 的 ID。
    /// </summary>
    /// <param name="drawableIndex">Drawable 索引</param>
    /// <returns>Drawable 的 ID</returns>
    public unsafe string GetDrawableId(int drawableIndex)
    {
        if (0 <= drawableIndex && drawableIndex < DrawableIds.Count)
        {
            throw new IndexOutOfRangeException("Out of DrawableIds size");
        }
        return DrawableIds[drawableIndex];
    }

    /// <summary>
    /// 获取 Drawable 的绘制顺序列表。
    /// </summary>
    /// <returns>Drawable 的绘制顺序列表</returns>
    public unsafe int* GetDrawableRenderOrders()
    {
        return CubismCore.GetDrawableRenderOrders(Model);
    }

    /// <summary>
    /// 获取 Drawable 的纹理索引（旧函数名已误，已添加替代函数 getDrawableTextureIndex，本函数已弃用）。
    /// </summary>
    /// <param name="drawableIndex">Drawable 索引</param>
    /// <returns>Drawable 的纹理索引</returns>
    public int GetDrawableTextureIndices(int drawableIndex)
    {
        return GetDrawableTextureIndex(drawableIndex);
    }

    /// <summary>
    /// 获取 Drawable 的纹理索引。
    /// </summary>
    /// <param name="drawableIndex">Drawable 索引</param>
    /// <returns>Drawable 的纹理索引</returns>
    public unsafe int GetDrawableTextureIndex(int drawableIndex)
    {
        var textureIndices = CubismCore.GetDrawableTextureIndices(Model);
        return textureIndices[drawableIndex];
    }

    /// <summary>
    /// 获取 Drawable 的顶点索引数量。
    /// </summary>
    /// <param name="drawableIndex">Drawable 索引</param>
    /// <returns>Drawable 的顶点索引数量</returns>
    public unsafe int GetDrawableVertexIndexCount(int drawableIndex)
    {
        return CubismCore.GetDrawableIndexCounts(Model)[drawableIndex];
    }

    /// <summary>
    /// 获取 Drawable 的顶点数量。
    /// </summary>
    /// <param name="drawableIndex">Drawable 索引</param>
    /// <returns>Drawable 的顶点数量</returns>
    public unsafe int GetDrawableVertexCount(int drawableIndex)
    {
        return CubismCore.GetDrawableVertexCounts(Model)[drawableIndex];
    }

    /// <summary>
    /// 获取 Drawable 的顶点列表。
    /// </summary>
    /// <param name="drawableIndex">Drawable 索引</param>
    /// <returns>Drawable 的顶点列表</returns>
    public unsafe float* GetDrawableVertices(int drawableIndex)
    {
        return (float*)GetDrawableVertexPositions(drawableIndex);
    }

    /// <summary>
    /// 获取 Drawable 的顶点索引列表。
    /// </summary>
    /// <param name="drawableIndex">Drawable 索引</param>
    /// <returns>Drawable 的顶点索引列表</returns>
    public unsafe ushort* GetDrawableVertexIndices(int drawableIndex)
    {
        return CubismCore.GetDrawableIndices(Model)[drawableIndex];
    }

    /// <summary>
    /// 获取 Drawable 的顶点列表（Vector2）。
    /// </summary>
    /// <param name="drawableIndex">Drawable 索引</param>
    /// <returns>Drawable 的顶点列表</returns>
    public unsafe Vector2* GetDrawableVertexPositions(int drawableIndex)
    {
        return CubismCore.GetDrawableVertexPositions(Model)[drawableIndex];
    }

    /// <summary>
    /// 获取 Drawable 的顶点 UV 列表。
    /// </summary>
    /// <param name="drawableIndex">Drawable 索引</param>
    /// <returns>Drawable 的顶点 UV 列表</returns>
    public unsafe Vector2* GetDrawableVertexUvs(int drawableIndex)
    {
        return CubismCore.GetDrawableVertexUvs(Model)[drawableIndex];
    }

    /// <summary>
    /// 获取 Drawable 的不透明度。
    /// </summary>
    /// <param name="drawableIndex">Drawable 索引</param>
    /// <returns>Drawable 的不透明度</returns>
    public unsafe float GetDrawableOpacity(int drawableIndex)
    {
        return CubismCore.GetDrawableOpacities(Model)[drawableIndex];
    }

    /// <summary>
    /// 获取 Drawable 的乘算色。
    /// </summary>
    /// <param name="drawableIndex">Drawable 索引</param>
    /// <returns>Drawable 的乘算色</returns>
    public unsafe Vector4 GetDrawableMultiplyColor(int drawableIndex)
    {
        return CubismCore.GetDrawableMultiplyColors(Model)[drawableIndex];
    }

    /// <summary>
    /// 获取 Drawable 的屏幕色。
    /// </summary>
    /// <param name="drawableIndex">Drawable 索引</param>
    /// <returns>Drawable 的屏幕色</returns>
    public unsafe Vector4 GetDrawableScreenColor(int drawableIndex)
    {
        return CubismCore.GetDrawableScreenColors(Model)[drawableIndex];
    }

    /// <summary>
    /// 获取 Drawable 的父部件索引。
    /// </summary>
    /// <param name="drawableIndex">Drawable 索引</param>
    /// <returns>Drawable 的父部件索引</returns>
    public unsafe int GetDrawableParentPartIndex(int drawableIndex)
    {
        return CubismCore.GetDrawableParentPartIndices(Model)[drawableIndex];
    }

    /// <summary>
    /// 获取 Drawable 的混合模式（Blend Mode）。
    /// </summary>
    /// <param name="drawableIndex">Drawable 索引</param>
    /// <returns>Drawable 的混合模式</returns>
    public unsafe CubismBlendMode GetDrawableBlendMode(int drawableIndex)
    {
        var constantFlags = CubismCore.GetDrawableConstantFlags(Model)[drawableIndex];
        return IsBitSet(constantFlags, CsmEnum.CsmBlendAdditive)
                   ? CubismBlendMode.Additive
                   : IsBitSet(constantFlags, CsmEnum.CsmBlendMultiplicative)
                   ? CubismBlendMode.Multiplicative
                   : CubismBlendMode.Normal;
    }

    /// <summary>
    /// 获取 Drawable 在使用遮罩时的反转设置。若不使用遮罩则忽略。
    /// </summary>
    /// <param name="drawableIndex">Drawable 索引</param>
    /// <returns>Drawable 的遮罩反转设置</returns>
    public unsafe bool GetDrawableInvertedMask(int drawableIndex)
    {
        var constantFlags = CubismCore.GetDrawableConstantFlags(Model)[drawableIndex];
        return IsBitSet(constantFlags, CsmEnum.CsmIsInvertedMask);
    }

    /// <summary>
    /// 获取 Drawable 的显示信息。
    /// </summary>
    /// <param name="drawableIndex">Drawable 索引</param>
    /// <returns>true    Drawable 可见
    /// false   Drawable 不可见</returns>
    public unsafe bool GetDrawableDynamicFlagIsVisible(int drawableIndex)
    {
        var dynamicFlags = CubismCore.GetDrawableDynamicFlags(Model)[drawableIndex];
        return IsBitSet(dynamicFlags, CsmEnum.CsmIsVisible);
    }

    /// <summary>
    /// 获取在最近一次 CubismModel::Update 调用中，Drawable 的显示状态是否发生变化。
    /// </summary>
    /// <param name="drawableIndex">Drawable 索引</param>
    /// <returns>true    Drawable 的显示状态在最近一次 Update 中发生变化
    /// false   Drawable 的显示状态在最近一次 Update 中未发生变化</returns>
    public unsafe bool GetDrawableDynamicFlagVisibilityDidChange(int drawableIndex)
    {
        var dynamicFlags = CubismCore.GetDrawableDynamicFlags(Model)[drawableIndex];
        return IsBitSet(dynamicFlags, CsmEnum.CsmVisibilityDidChange);
    }

    /// <summary>
    /// 获取在最近一次 CubismModel::Update 调用中，Drawable 的不透明度是否发生变化。
    /// </summary>
    /// <param name="drawableIndex">Drawable 索引</param>
    /// <returns>true    Drawable 的不透明度在最近一次 Update 中发生变化
    /// false   Drawable 的不透明度在最近一次 Update 中未发生变化</returns>
    public unsafe bool GetDrawableDynamicFlagOpacityDidChange(int drawableIndex)
    {
        var dynamicFlags = CubismCore.GetDrawableDynamicFlags(Model)[drawableIndex];
        return IsBitSet(dynamicFlags, CsmEnum.CsmOpacityDidChange);
    }

    /// <summary>
    /// 获取在最近一次 CubismModel::Update 调用中，Drawable 的 DrawOrder 是否发生变化。
    /// DrawOrder 在 ArtMesh 上指定，取值范围为 0 到 1000。
    /// </summary>
    /// <param name="drawableIndex">Drawable 索引</param>
    /// <returns>true    Drawable 的 DrawOrder 在最近一次 Update 中发生变化
    /// false   Drawable 的 DrawOrder 在最近一次 Update 中未发生变化</returns>
    public unsafe bool GetDrawableDynamicFlagDrawOrderDidChange(int drawableIndex)
    {
        var dynamicFlags = CubismCore.GetDrawableDynamicFlags(Model)[drawableIndex];
        return IsBitSet(dynamicFlags, CsmEnum.CsmDrawOrderDidChange);
    }

    /// <summary>
    /// 获取在最近一次 CubismModel::Update 调用中，Drawable 的渲染顺序是否发生变化。
    /// </summary>
    /// <param name="drawableIndex">Drawable 索引</param>
    /// <returns>true    Drawable 的渲染顺序在最近一次 Update 中发生变化
    /// false   Drawable 的渲染顺序在最近一次 Update 中未发生变化</returns>
    public unsafe bool GetDrawableDynamicFlagRenderOrderDidChange(int drawableIndex)
    {
        var dynamicFlags = CubismCore.GetDrawableDynamicFlags(Model)[drawableIndex];
        return IsBitSet(dynamicFlags, CsmEnum.CsmRenderOrderDidChange);
    }

    /// <summary>
    /// 获取在最近一次 CubismModel::Update 调用中，Drawable 的顶点信息是否发生变化。
    /// </summary>
    /// <param name="drawableIndex">Drawable 索引</param>
    /// <returns>true    Drawable 的顶点信息在最近一次 Update 中发生变化
    /// false   Drawable 的顶点信息在最近一次 Update 中未发生变化</returns>
    public unsafe bool GetDrawableDynamicFlagVertexPositionsDidChange(int drawableIndex)
    {
        var dynamicFlags = CubismCore.GetDrawableDynamicFlags(Model)[drawableIndex];
        return IsBitSet(dynamicFlags, CsmEnum.CsmVertexPositionsDidChange);
    }

    /// <summary>
    /// 获取在最近一次 CubismModel::Update 调用中，Drawable 的乘算色或屏幕色是否发生变化。
    /// </summary>
    /// <param name="drawableIndex">Drawable 索引</param>
    /// <returns>true    Drawable 的乘算色/屏幕色在最近一次 Update 中发生变化
    /// false   Drawable 的乘算色/屏幕色在最近一次 Update 中未发生变化</returns>
    public unsafe bool GetDrawableDynamicFlagBlendColorDidChange(int drawableIndex)
    {
        var dynamicFlags = CubismCore.GetDrawableDynamicFlags(Model)[drawableIndex];
        return IsBitSet(dynamicFlags, CsmEnum.CsmBlendColorDidChange);
    }

    /// <summary>
    /// 获取 Drawable 的裁剪掩码列表。
    /// </summary>
    /// <returns>Drawable 的裁剪掩码列表</returns>
    public unsafe int** GetDrawableMasks()
    {
        return CubismCore.GetDrawableMasks(Model);
    }

    /// <summary>
    /// 获取 Drawable 的裁剪掩码数量列表。
    /// </summary>
    /// <returns>Drawable 的裁剪掩码数量列表</returns>
    public unsafe int* GetDrawableMaskCounts()
    {
        return CubismCore.GetDrawableMaskCounts(Model);
    }

    /// <summary>
    /// 是否使用裁剪掩码？
    /// </summary>
    /// <returns>true    使用裁剪掩码
    /// false   未使用裁剪掩码</returns>
    public unsafe bool IsUsingMasking()
    {
        for (int d = 0; d < CubismCore.GetDrawableCount(Model); ++d)
        {
            if (CubismCore.GetDrawableMaskCounts(Model)[d] <= 0)
            {
                continue;
            }
            return true;
        }

        return false;
    }

    /// <summary>
    /// 读取已保存的参数
    /// </summary>
    public unsafe void LoadParameters()
    {
        int parameterCount = CubismCore.GetParameterCount(Model);
        int savedParameterCount = _savedParameters.Count;

        if (parameterCount > savedParameterCount)
        {
            parameterCount = savedParameterCount;
        }

        for (int i = 0; i < parameterCount; ++i)
        {
            _parameterValues[i] = _savedParameters[i];
        }
    }

    /// <summary>
    /// 保存参数。
    /// </summary>
    public unsafe void SaveParameters()
    {
        int parameterCount = CubismCore.GetParameterCount(Model);
        int savedParameterCount = _savedParameters.Count;

        if (savedParameterCount != parameterCount)
        {
            _savedParameters.Clear();
            for (int i = 0; i < parameterCount; ++i)
            {
                _savedParameters.Add(_parameterValues[i]);
            }
        }
        else
        {
            for (int i = 0; i < parameterCount; ++i)
            {
                _savedParameters[i] = _parameterValues[i];
            }
        }
    }

    /// <summary>
    /// 获取 Drawable 的乘算色（外部可覆盖）。
    /// </summary>
    public CubismTextureColor GetMultiplyColor(int drawableIndex)
    {
        if (GetOverwriteFlagForModelMultiplyColors() ||
            GetOverwriteFlagForDrawableMultiplyColors(drawableIndex))
        {
            return _userMultiplyColors[drawableIndex].Color;
        }

        var color = GetDrawableMultiplyColor(drawableIndex);

        return new CubismTextureColor(color.X, color.Y, color.Z, color.W);
    }

    /// <summary>
    /// 获取 Drawable 的屏幕色（外部可覆盖）。
    /// </summary>
    public CubismTextureColor GetScreenColor(int drawableIndex)
    {
        if (GetOverwriteFlagForModelScreenColors() ||
            GetOverwriteFlagForDrawableScreenColors(drawableIndex))
        {
            return _userScreenColors[drawableIndex].Color;
        }

        var color = GetDrawableScreenColor(drawableIndex);
        return new CubismTextureColor(color.X, color.Y, color.Z, color.W);
    }

    /// <summary>
    /// 设置 Drawable 的乘算色
    /// </summary>
    public void SetMultiplyColor(int drawableIndex, CubismTextureColor color)
    {
        SetMultiplyColor(drawableIndex, color.R, color.G, color.B, color.A);
    }

    /// <summary>
    /// 设置 Drawable 的乘算色
    /// </summary>
    public void SetMultiplyColor(int drawableIndex, float r, float g, float b, float a = 1.0f)
    {
        _userMultiplyColors[drawableIndex].Color.R = r;
        _userMultiplyColors[drawableIndex].Color.G = g;
        _userMultiplyColors[drawableIndex].Color.B = b;
        _userMultiplyColors[drawableIndex].Color.A = a;
    }

    /// <summary>
    /// 设置 Drawable 的屏幕色
    /// </summary>
    /// <param name="drawableIndex"></param>
    /// <param name="color"></param>
    public void SetScreenColor(int drawableIndex, CubismTextureColor color)
    {
        SetScreenColor(drawableIndex, color.R, color.G, color.B, color.A);
    }

    /// <summary>
    /// 设置 Drawable 的屏幕色
    /// </summary>
    public void SetScreenColor(int drawableIndex, float r, float g, float b, float a = 1.0f)
    {
        _userScreenColors[drawableIndex].Color.R = r;
        _userScreenColors[drawableIndex].Color.G = g;
        _userScreenColors[drawableIndex].Color.B = b;
        _userScreenColors[drawableIndex].Color.A = a;
    }

    /// <summary>
    /// 获取部件的乘算色
    /// </summary>
    public CubismTextureColor GetPartMultiplyColor(int partIndex)
    {
        return _userPartMultiplyColors[partIndex].Color;
    }

    /// <summary>
    /// 获取部件的屏幕色
    /// </summary>
    public CubismTextureColor GetPartScreenColor(int partIndex)
    {
        return _userPartScreenColors[partIndex].Color;
    }

    /// <summary>
    /// 设置部件的乘算色
    /// </summary>
    public void SetPartMultiplyColor(int partIndex, CubismTextureColor color)
    {
        SetPartMultiplyColor(partIndex, color.R, color.G, color.B, color.A);
    }

    /// <summary>
    /// 设置部件的乘算色
    /// </summary>
    public void SetPartMultiplyColor(int partIndex, float r, float g, float b, float a = 1.0f)
    {
        SetPartColor(partIndex, r, g, b, a, _userPartMultiplyColors, _userMultiplyColors);
    }

    /// <summary>
    /// 设置部件的屏幕色
    /// </summary>
    public void SetPartScreenColor(int partIndex, CubismTextureColor color)
    {
        SetPartScreenColor(partIndex, color.R, color.G, color.B, color.A);
    }

    /// <summary>
    /// 设置部件的屏幕色
    /// </summary>
    /// <param name="a"></param>
    public void SetPartScreenColor(int partIndex, float r, float g, float b, float a = 1.0f)
    {
        SetPartColor(partIndex, r, g, b, a, _userPartScreenColors, _userScreenColors);
    }

    /// <summary>
    /// 是否用 SDK 的颜色信息覆盖模型整体的乘算色？
    /// </summary>
    /// <returns>true -> 使用 SDK 上的颜色信息
    /// false -> 使用模型自身的颜色信息</returns>
    public bool GetOverwriteFlagForModelMultiplyColors()
    {
        return _isOverwrittenModelMultiplyColors;
    }

    /// <summary>
    /// 是否用 SDK 的颜色信息覆盖模型整体的屏幕色？
    /// </summary>
    /// <returns>true -> 使用 SDK 上的颜色信息
    /// false -> 使用模型自身的颜色信息</returns>
    public bool GetOverwriteFlagForModelScreenColors()
    {
        return _isOverwrittenModelScreenColors;
    }

    /// <summary>
    /// 设置是否用 SDK 的颜色信息覆盖模型整体的乘算色（true 使用 SDK，false 使用模型）。
    /// </summary>
    public void SetOverwriteFlagForModelMultiplyColors(bool value)
    {
        _isOverwrittenModelMultiplyColors = value;
    }

    /// <summary>
    /// 设置是否用 SDK 的颜色信息覆盖模型整体的屏幕色（true 使用 SDK，false 使用模型）。
    /// </summary>
    public void SetOverwriteFlagForModelScreenColors(bool value)
    {
        _isOverwrittenModelScreenColors = value;
    }

    /// <summary>
    /// 是否用 SDK 的颜色信息覆盖 drawable 的乘算色？
    /// </summary>
    /// <returns>true -> 使用 SDK 上的颜色信息
    /// false -> 使用模型自身的颜色信息</returns>
    public bool GetOverwriteFlagForDrawableMultiplyColors(int drawableIndex)
    {
        return _userMultiplyColors[drawableIndex].IsOverwritten;
    }

    /// <summary>
    /// 是否用 SDK 的颜色信息覆盖 drawable 的屏幕色？
    /// </summary>
    /// <returns>true -> 使用 SDK 上的颜色信息
    /// false -> 使用模型自身的颜色信息</returns>
    public bool GetOverwriteFlagForDrawableScreenColors(int drawableIndex)
    {
        return _userScreenColors[drawableIndex].IsOverwritten;
    }

    /// <summary>
    /// 设置是否用 SDK 的颜色信息覆盖 drawable 的乘算色（true 使用 SDK，false 使用模型）。
    /// </summary>
    public void SetOverwriteFlagForDrawableMultiplyColors(int drawableIndex, bool value)
    {
        _userMultiplyColors[drawableIndex].IsOverwritten = value;
    }

    /// <summary>
    /// 设置是否用 SDK 的颜色信息覆盖 drawable 的屏幕色（true 使用 SDK，false 使用模型）。
    /// </summary>
    public void SetOverwriteFlagForDrawableScreenColors(int drawableIndex, bool value)
    {
        _userScreenColors[drawableIndex].IsOverwritten = value;
    }

    /// <summary>
    /// 是否用 SDK 的颜色信息覆盖 part 的乘算色。
    /// </summary>
    /// <returns>true → 使用 SDK 颜色信息
    /// false → 使用模型颜色信息</returns>
    public bool GetOverwriteColorForPartMultiplyColors(int partIndex)
    {
        return _userPartMultiplyColors[partIndex].IsOverwritten;
    }

    /// <summary>
    /// 是否用 SDK 的颜色信息覆盖 part 的屏幕色（true 使用 SDK，false 使用模型）。
    /// </summary>
    public bool GetOverwriteColorForPartScreenColors(int partIndex)
    {
        return _userPartScreenColors[partIndex].IsOverwritten;
    }

    /// <summary>
    /// 获取 Drawable 的剔除信息。
    /// </summary>
    /// <param name="drawableIndex">Drawable 的索引</param>
    /// <returns>Drawable 的剔除信息</returns>
    public unsafe bool GetDrawableCulling(int drawableIndex)
    {
        if (GetOverwriteFlagForModelCullings() || GetOverwriteFlagForDrawableCullings(drawableIndex))
        {
            return _userCullings[drawableIndex].IsCulling;
        }

        var constantFlags = CubismCore.GetDrawableConstantFlags(Model);
        return !IsBitSet(constantFlags[drawableIndex], CsmEnum.CsmIsDoubleSided);
    }

    /// <summary>
    /// 设置 Drawable 的剔除信息。
    /// </summary>
    public void SetDrawableCulling(int drawableIndex, bool isCulling)
    {
        _userCullings[drawableIndex].IsCulling = isCulling;
    }

    /// <summary>
    /// 是否用 SDK 覆盖整个模型的剔除设置。
    /// </summary>
    /// <returns>true → 使用 SDK 的剔除设置
    /// false → 使用模型的剔除设置</returns>
    public bool GetOverwriteFlagForModelCullings()
    {
        return _isOverwrittenCullings;
    }

    /// <summary>
    /// 设置是否用 SDK 覆盖整个模型的剔除设置（true 使用 SDK，false 使用模型）。
    /// </summary>
    public void SetOverwriteFlagForModelCullings(bool value)
    {
        _isOverwrittenCullings = value;
    }

    /// <summary>
    /// 是否用 SDK 覆盖 drawable 的剔除设置。
    /// </summary>
    /// <returns>true → 使用 SDK 的剔除设置
    /// false → 使用模型的剔除设置</returns>
    public bool GetOverwriteFlagForDrawableCullings(int drawableIndex)
    {
        return _userCullings[drawableIndex].IsOverwritten;
    }

    /// <summary>
    /// 设置是否用 SDK 覆盖 drawable 的剔除设置（true 使用 SDK，false 使用模型）。
    /// </summary>
    public void SetOverwriteFlagForDrawableCullings(int drawableIndex, bool value)
    {
        _userCullings[drawableIndex].IsOverwritten = value;
    }

    /// <summary>
    /// 获取模型的不透明度。
    /// </summary>
    /// <returns>不透明度的值</returns>
    public float GetModelOpacity()
    {
        return _modelOpacity;
    }

    /// <summary>
    /// 设置模型的不透明度。
    /// </summary>
    /// <param name="value">不透明度的值</param>
    public void SetModelOpacity(float value)
    {
        _modelOpacity = value;
    }

    private static bool IsBitSet(byte data, byte mask)
    {
        return (data & mask) == mask;
    }

    public void SetOverwriteColorForPartMultiplyColors(int partIndex, bool value)
    {
        _userPartMultiplyColors[partIndex].IsOverwritten = value;
        SetOverwriteColorForPartColors(partIndex, value, _userPartMultiplyColors, _userMultiplyColors);
    }

    public void SetOverwriteColorForPartScreenColors(int partIndex, bool value)
    {
        _userPartScreenColors[partIndex].IsOverwritten = value;
        SetOverwriteColorForPartColors(partIndex, value, _userPartScreenColors, _userScreenColors);
    }

    /// <summary>
    /// 获取参数的 ID。
    /// </summary>
    /// <param name="parameterIndex">参数的索引</param>
    /// <returns>参数的 ID</returns>
    public unsafe string GetParameterId(int parameterIndex)
    {
        if (0 <= parameterIndex && parameterIndex < ParameterIds.Count)
        {
            throw new IndexOutOfRangeException("Out of ParameterIds size");
        }
        return ParameterIds[parameterIndex];
    }
}
