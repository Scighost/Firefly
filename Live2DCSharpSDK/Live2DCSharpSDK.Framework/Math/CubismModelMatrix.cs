namespace Live2DCSharpSDK.Framework.Math;

/// <summary>
/// 用于模型坐标设置的 4x4 矩阵类。
/// </summary>
public record CubismModelMatrix : CubismMatrix44
{
    public const string KeyWidth = "width";
    public const string KeyHeight = "height";
    public const string KeyX = "x";
    public const string KeyY = "y";
    public const string KeyCenterX = "center_x";
    public const string KeyCenterY = "center_y";
    public const string KeyTop = "top";
    public const string KeyBottom = "bottom";
    public const string KeyLeft = "left";
    public const string KeyRight = "right";

    /// <summary>
    /// 宽度
    /// </summary>
    private readonly float _width;
    /// <summary>
    /// 高度
    /// </summary>
    private readonly float _height;

    public CubismModelMatrix(float w, float h)
    {
        _width = w;
        _height = h;

        SetHeight(2.0f);
    }

    /// <summary>
    /// 设置宽度。
    /// </summary>
    /// <param name="w">宽度</param>
    public void SetWidth(float w)
    {
        float scaleX = w / _width;
        float scaleY = scaleX;
        Scale(scaleX, scaleY);
    }

    /// <summary>
    /// 设置高度。
    /// </summary>
    /// <param name="h">高度</param>
    public void SetHeight(float h)
    {
        float scaleX = h / _height;
        float scaleY = scaleX;
        Scale(scaleX, scaleY);
    }

    /// <summary>
    /// 设置位置。
    /// </summary>
    /// <param name="x">X 轴的位置</param>
    /// <param name="y">Y 轴的位置</param>
    public void SetPosition(float x, float y)
    {
        Translate(x, y);
    }

    /// <summary>
    /// 设置中心位置。
    /// </summary>
    /// <param name="x">X 轴的中心位置</param>
    /// <param name="y">Y 轴的中心位置</param>
    public void SetCenterPosition(float x, float y)
    {
        CenterX(x);
        CenterY(y);
    }

    /// <summary>
    /// 设置上边的位置。
    /// </summary>
    /// <param name="y">上边的 Y 轴位置</param>
    public void Top(float y)
    {
        SetY(y);
    }

    /// <summary>
    /// 设置下边的位置。
    /// </summary>
    /// <param name="y">下边的 Y 轴位置</param>
    public void Bottom(float y)
    {
        float h = _height * GetScaleY();
        TranslateY(y - h);
    }

    /// <summary>
    /// 设置左边的位置。
    /// </summary>
    /// <param name="x">左边的 X 轴位置</param>
    public void Left(float x)
    {
        SetX(x);
    }

    /// <summary>
    /// 设置右边的位置。
    /// </summary>
    /// <param name="x">右边的 X 轴位置</param>
    public void Right(float x)
    {
        float w = _width * GetScaleX();
        TranslateX(x - w);
    }

    /// <summary>
    /// 设置 X 轴的中心位置。
    /// </summary>
    /// <param name="x">X 轴的中心位置</param>
    public void CenterX(float x)
    {
        float w = _width * GetScaleX();
        TranslateX(x - (w / 2.0f));
    }

    /// <summary>
    /// 设置 X 轴的位置。
    /// </summary>
    /// <param name="x">X 轴的位置</param>
    public void SetX(float x)
    {
        TranslateX(x);
    }

    /// <summary>
    /// 设置 Y 轴的中心位置。
    /// </summary>
    /// <param name="y">Y 轴的中心位置</param>
    public void CenterY(float y)
    {
        float h = _height * GetScaleY();
        TranslateY(y - (h / 2.0f));
    }

    /// <summary>
    /// 设置 Y 轴的位置。
    /// </summary>
    /// <param name="y">Y 轴的位置</param>
    public void SetY(float y)
    {
        TranslateY(y);
    }

    /// <summary>
    /// 根据布局信息设置位置。
    /// </summary>
    /// <param name="layout">布局信息</param>
    public void SetupFromLayout(Dictionary<string, float> layout)
    {
        foreach (var item in layout)
        {
            if (item.Key == KeyWidth)
            {
                SetWidth(item.Value);
            }
            else if (item.Key == KeyHeight)
            {
                SetHeight(item.Value);
            }
        }

        foreach (var item in layout)
        {
            if (item.Key == KeyX)
            {
                SetX(item.Value);
            }
            else if (item.Key == KeyY)
            {
                SetY(item.Value);
            }
            else if (item.Key == KeyCenterX)
            {
                CenterX(item.Value);
            }
            else if (item.Key == KeyCenterY)
            {
                CenterY(item.Value);
            }
            else if (item.Key == KeyTop)
            {
                Top(item.Value);
            }
            else if (item.Key == KeyBottom)
            {
                Bottom(item.Value);
            }
            else if (item.Key == KeyLeft)
            {
                Left(item.Value);
            }
            else if (item.Key == KeyRight)
            {
                Right(item.Value);
            }
        }
    }
}
