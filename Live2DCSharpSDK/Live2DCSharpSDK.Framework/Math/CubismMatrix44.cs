namespace Live2DCSharpSDK.Framework.Math;

/// <summary>
/// 用于4x4矩阵的实用类。
/// </summary>
public record CubismMatrix44
{
    private readonly float[] Ident =
    [
        1.0f, 0.0f, 0.0f, 0.0f,
        0.0f, 1.0f, 0.0f, 0.0f,
        0.0f, 0.0f, 1.0f, 0.0f,
        0.0f, 0.0f, 0.0f, 1.0f
    ];

    private readonly float[] _mpt1 =
    [
        1.0f, 0.0f, 0.0f, 0.0f,
        0.0f, 1.0f, 0.0f, 0.0f,
        0.0f, 0.0f, 1.0f, 0.0f,
        0.0f, 0.0f, 0.0f, 1.0f
    ];

    private readonly float[] _mpt2 =
    [
        1.0f, 0.0f, 0.0f, 0.0f,
        0.0f, 1.0f, 0.0f, 0.0f,
        0.0f, 0.0f, 1.0f, 0.0f,
        0.0f, 0.0f, 0.0f, 1.0f
    ];

    /// <summary>
    /// 4x4 矩阵数据
    /// </summary>
    protected float[] _tr = new float[16];

    public float[] Tr => _tr;

    /// <summary>
    /// 构造函数。
    /// </summary>
    public CubismMatrix44()
    {
        LoadIdentity();
    }

    /// <summary>
    /// 初始化为单位矩阵。
    /// </summary>
    public void LoadIdentity()
    {
        SetMatrix(Ident);
    }

    /// <summary>
    /// 设置矩阵。
    /// </summary>
    /// <param name="tr">用 16 个浮点数表示的 4x4 矩阵</param>
    public void SetMatrix(float[] tr)
    {
        Array.Copy(tr, _tr, 16);
    }

    public void SetMatrix(CubismMatrix44 tr)
    {
        SetMatrix(tr.Tr);
    }

    /// <summary>
    /// 获取 X 轴的缩放比例。
    /// </summary>
    /// <returns>X 轴的缩放比例</returns>
    public float GetScaleX()
    {
        return _tr[0];
    }

    /// <summary>
    /// 获取 Y 轴的缩放比例。
    /// </summary>
    /// <returns>Y 轴的缩放比例</returns>
    public float GetScaleY()
    {
        return _tr[5];
    }

    /// <summary>
    /// 获取 X 轴的平移量。
    /// </summary>
    /// <returns>X 轴的平移量</returns>
    public float GetTranslateX()
    {
        return _tr[12];
    }

    /// <summary>
    /// 获取 Y 轴的平移量。
    /// </summary>
    /// <returns>Y 轴的平移量</returns>
    public float GetTranslateY()
    {
        return _tr[13];
    }

    /// <summary>
    /// 使用当前矩阵变换 X 轴的值。
    /// </summary>
    /// <param name="src">X 轴的值</param>
    /// <returns>经过当前矩阵计算得到的 X 轴值</returns>
    public float TransformX(float src)
    {
        return _tr[0] * src + _tr[12];
    }

    /// <summary>
    /// 使用当前矩阵变换 Y 轴的值。
    /// </summary>
    /// <param name="src">Y 轴的值</param>
    /// <returns>经过当前矩阵计算得到的 Y 轴值</returns>
    public float TransformY(float src)
    {
        return _tr[5] * src + _tr[13];
    }

    /// <summary>
    /// 使用当前矩阵逆变换 X 轴的值。
    /// </summary>
    /// <param name="src">X 轴的值</param>
    /// <returns>由当前矩阵逆变换得到的 X 轴值</returns>
    public float InvertTransformX(float src)
    {
        return (src - _tr[12]) / _tr[0];
    }

    /// <summary>
    /// 使用当前矩阵逆变换 Y 轴的值。
    /// </summary>
    /// <param name="src">Y 轴的值</param>
    /// <returns>由当前矩阵逆变换得到的 Y 轴值</returns>
    public float InvertTransformY(float src)
    {
        return (src - _tr[13]) / _tr[5];
    }

    /// <summary>
    /// 以当前矩阵的位置为基准进行相对平移。
    /// </summary>
    /// <param name="x">X 轴的移动量</param>
    /// <param name="y">Y 轴的移动量</param>
    public void TranslateRelative(float x, float y)
    {
        _mpt1[12] = x;
        _mpt1[13] = y;
        MultiplyByMatrix(_mpt1);
    }

    /// <summary>
    /// 将当前矩阵的位置设置为指定位置。
    /// </summary>
    /// <param name="x">X 轴的移动量</param>
    /// <param name="y">Y 轴的移动量</param>
    public void Translate(float x, float y)
    {
        _tr[12] = x;
        _tr[13] = y;
    }

    /// <summary>
    /// 将当前矩阵的 X 轴位置设置为指定位置。
    /// </summary>
    /// <param name="x">X 轴的移动量</param>
    public void TranslateX(float x)
    {
        _tr[12] = x;
    }

    /// <summary>
    /// 将当前矩阵的 Y 轴位置设置为指定位置。
    /// </summary>
    /// <param name="y">Y 轴的移动量</param>
    public void TranslateY(float y)
    {
        _tr[13] = y;
    }

    /// <summary>
    /// 相对设置当前矩阵的缩放比例。
    /// </summary>
    /// <param name="x">X 轴的缩放比例</param>
    /// <param name="y">Y 轴的缩放比例</param>
    public void ScaleRelative(float x, float y)
    {
        _mpt2[0] = x;
        _mpt2[5] = y;
        MultiplyByMatrix(_mpt2);
    }

    /// <summary>
    /// 将当前矩阵的缩放比例设置为指定值。
    /// </summary>
    /// <param name="x">X 轴的缩放比例</param>
    /// <param name="y">Y 轴的缩放比例</param>
    public void Scale(float x, float y)
    {
        _tr[0] = x;
        _tr[5] = y;
    }

    public float[] _mpt = new float[16];

    public void MultiplyByMatrix(float[] a)
    {
        Array.Fill(_mpt, 0);

        int n = 4;

        for (int i = 0; i < n; ++i)
        {
            for (int j = 0; j < n; ++j)
            {
                for (int k = 0; k < n; ++k)
                {
                    _mpt[j + i * 4] += a[k + i * 4] * _tr[j + k * 4];
                }
            }
        }

        Array.Copy(_mpt, _tr, 16);
    }

    /// <summary>
    /// 将矩阵与当前矩阵相乘。
    /// </summary>
    /// <param name="m">矩阵</param>
    public void MultiplyByMatrix(CubismMatrix44 m)
    {
        MultiplyByMatrix(m.Tr);
    }
}
