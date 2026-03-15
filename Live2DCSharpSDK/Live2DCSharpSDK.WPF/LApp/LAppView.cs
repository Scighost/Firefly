using Live2DCSharpSDK.Framework;
using Live2DCSharpSDK.Framework.Math;

namespace Live2DCSharpSDK.WPF.LApp;

/// <summary>
/// 渲染类
/// </summary>
/// <remarks>
/// 构造函数
/// </remarks>
public abstract class LAppView(LAppDelegate lapp)
{
    /// <summary>
    /// 触控管理器
    /// </summary>
    private readonly TouchManager _touchManager = new();
    /// <summary>
    /// 设备到屏幕的矩阵
    /// </summary>
    private readonly CubismMatrix44 _deviceToScreen = new();
    /// <summary>
    /// viewMatrix
    /// </summary>
    private readonly CubismViewMatrix _viewMatrix = new();

    public abstract void RenderPre();
    public abstract void RenderPost();

    /// <summary>
    /// 初始化。
    /// </summary>
    public void Initialize()
    {
        int width = lapp.WindowWidth;
        int height = lapp.WindowHeight;
        if (width == 0 || height == 0)
        {
            return;
        }

        // 匹配窗口纵横比的逻辑矩形
        float ratio = (float)width / height;
        float left = -ratio;
        float right = ratio;
        float bottom = LAppDefine.ViewLogicalBottom;
        float top = LAppDefine.ViewLogicalTop;

        _viewMatrix.SetScreenRect(left, right, bottom, top); // 与设备对应的屏幕范围。X 左端, X 右端, Y 下端, Y 上端
        _viewMatrix.Scale(LAppDefine.ViewScale, LAppDefine.ViewScale);

        _deviceToScreen.LoadIdentity(); // 尺寸变更时必须重置
        if (width > height)
        {
            float screenW = MathF.Abs(right - left);
            _deviceToScreen.ScaleRelative(screenW / width, -screenW / width);
        }
        else
        {
            float screenH = MathF.Abs(top - bottom);
            _deviceToScreen.ScaleRelative(screenH / height, -screenH / height);
        }
        _deviceToScreen.TranslateRelative(-width * 0.5f, -height * 0.5f);

        // 设置显示范围
        _viewMatrix.MaxScale = LAppDefine.ViewMaxScale; // 最大缩放倍率
        _viewMatrix.MinScale = LAppDefine.ViewMinScale; // 最小缩放倍率

        // 可显示的最大范围
        _viewMatrix.SetMaxScreenRect(
            LAppDefine.ViewLogicalMaxLeft,
            LAppDefine.ViewLogicalMaxRight,
            LAppDefine.ViewLogicalMaxBottom,
            LAppDefine.ViewLogicalMaxTop
        );
    }


    /// <summary>
    /// 绘制。
    /// </summary>
    internal void Render()
    {
        RenderPre();

        var manager = lapp.Live2dManager;
        manager.ViewMatrix.SetMatrix(_viewMatrix);

        // Cubism 更新·绘制
        manager.OnUpdate();

        RenderPost();
    }

    /// <summary>
    /// 触摸时调用。
    /// </summary>
    /// <param name="pointX">屏幕 X 坐标</param>
    /// <param name="pointY">屏幕 Y 坐标</param>
    public void OnTouchesBegan(float pointX, float pointY)
    {
        _touchManager.TouchesBegan(pointX, pointY);
        CubismLog.Debug($"[Live2D App]touchesBegan x:{pointX:#.##} y:{pointY:#.##}");
    }

    /// <summary>
    /// 触摸并移动指针时调用。
    /// </summary>
    /// <param name="pointX">屏幕 X 坐标</param>
    /// <param name="pointY">屏幕 Y 坐标</param>
    public void OnTouchesMoved(float pointX, float pointY)
    {
        float viewX = TransformViewX(_touchManager.GetX());
        float viewY = TransformViewY(_touchManager.GetY());

        _touchManager.TouchesMoved(pointX, pointY);

        lapp.Live2dManager.OnDrag(viewX, viewY);
    }

    /// <summary>
    /// 触摸结束时调用。
    /// </summary>
    /// <param name="pointX">屏幕 X 坐标</param>
    /// <param name="pointY">屏幕 Y 坐标</param>
    public void OnTouchesEnded(float _, float __)
    {
        // 触摸结束
        var live2DManager = lapp.Live2dManager;
        live2DManager.OnDrag(0.0f, 0.0f);
        // 单击
        float x = _deviceToScreen.TransformX(_touchManager.GetX()); // 获取转换为逻辑坐标后的值。
        float y = _deviceToScreen.TransformY(_touchManager.GetY()); // 获取转换为逻辑坐标后的值。
        CubismLog.Debug($"[Live2D App]touchesEnded x:{x:#.##} y:{y:#.##}");
        live2DManager.OnTap(x, y);
    }

    /// <summary>
    /// 将 X 坐标转换为 View 坐标。
    /// </summary>
    /// <param name="deviceX">设备 X 坐标</param>
    public float TransformViewX(float deviceX)
    {
        float screenX = _deviceToScreen.TransformX(deviceX); // 获取转换为逻辑坐标后的值。
        return _viewMatrix.InvertTransformX(screenX); // 缩放、移动后的值。
    }

    /// <summary>
    /// 将 Y 坐标转换为 View 坐标。
    /// </summary>
    /// <param name="deviceY">设备 Y 坐标</param>
    public float TransformViewY(float deviceY)
    {
        float screenY = _deviceToScreen.TransformY(deviceY); // 获取转换为逻辑坐标后的值。
        return _viewMatrix.InvertTransformY(screenY); // 缩放、移动后的值。
    }

    /// <summary>
    /// 将 X 坐标转换为 Screen 坐标。
    /// </summary>
    /// <param name="deviceX">设备 X 坐标</param>
    public float TransformScreenX(float deviceX)
    {
        return _deviceToScreen.TransformX(deviceX);
    }

    /// <summary>
    /// 将 Y 坐标转换为 Screen 坐标。
    /// </summary>
    /// <param name="deviceY">设备 Y 坐标</param>
    public float TransformScreenY(float deviceY)
    {
        return _deviceToScreen.TransformY(deviceY);
    }

    /// <summary>
    /// 将模型绘制到其他渲染目标的示例，
    /// 决定绘制时的 α 值
    /// </summary>
    public static float GetSpriteAlpha(int assign)
    {
        // 根据 assign 的数值适当决定
        float alpha = 0.25f + assign * 0.5f; // 作为示例，给 α 赋予适当的差值
        if (alpha > 1.0f)
        {
            alpha = 1.0f;
        }
        if (alpha < 0.1f)
        {
            alpha = 0.1f;
        }

        return alpha;
    }
}
