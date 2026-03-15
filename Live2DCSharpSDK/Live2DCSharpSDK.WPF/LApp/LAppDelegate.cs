using Live2DCSharpSDK.Framework.Model;
using Live2DCSharpSDK.Framework.Rendering;

namespace Live2DCSharpSDK.WPF.LApp;

/// <summary>
/// 应用程序类。
/// 管理 Cubism SDK。
/// </summary>
public abstract class LAppDelegate : IDisposable
{
    /// <summary>
    /// 纹理管理器。
    /// </summary>
    public LAppTextureManager TextureManager { get; private set; }

    public LAppLive2DManager Live2dManager { get; private set; }

    /// <summary>
    /// View 信息。
    /// </summary>
    public LAppView View { get; protected set; }

    public CubismTextureColor BGColor { get; set; } = new(0, 0, 0, 0);

    /// <summary>
    /// 是否正在点击。
    /// </summary>
    private bool _captured;
    /// <summary>
    /// 鼠标 X 坐标。
    /// </summary>
    private float _mouseX;
    /// <summary>
    /// 鼠标 Y 坐标。
    /// </summary>
    private float _mouseY;

    /// <summary>
    /// Initialize 函数中设置的窗口宽度。
    /// </summary>
    public int WindowWidth { get; protected set; }
    /// <summary>
    /// Initialize 函数中设置的窗口高度。
    /// </summary>
    public int WindowHeight { get; protected set; }

    public abstract void OnUpdatePre();
    public abstract void GetWindowSize(out int width, out int height);
    public abstract CubismRenderer CreateRenderer(CubismModel model);
    public abstract TextureInfo CreateTexture(LAppModel model, int index, int width, int height, IntPtr data);

    /// <summary>
    /// Rebind an already-created texture to a specific model's renderer.
    /// Called when the texture manager finds the texture in cache for a different model.
    /// </summary>
    public virtual void RebindTexture(LAppModel model, int index, TextureInfo info) { }

    public void InitApp()
    {
        TextureManager = new LAppTextureManager(this);

        // 记录窗口尺寸
        GetWindowSize(out int width, out int height);
        WindowWidth = width;
        WindowHeight = height;
        //初始化 AppView
        View.Initialize();

        //load model
        Live2dManager = new LAppLive2DManager(this);

        LAppPal.DeltaTime = 0;
    }

    /// <summary>
    /// 释放资源。
    /// </summary>
    public void Dispose()
    {
        Live2dManager.Dispose();
    }

    public void Resize()
    {
        GetWindowSize(out int width, out int height);
        if ((WindowWidth != width || WindowHeight != height) && width > 0 && height > 0)
        {
            // 保存尺寸
            WindowWidth = width;
            WindowHeight = height;
            //初始化 AppView
            View.Initialize();
        }
    }

    /// <summary>
    /// Need skip
    /// </summary>
    /// <returns></returns>
    public abstract bool RunPre();
    public abstract void RunPost();

    /// <summary>
    /// 执行处理。
    /// </summary>
    public void Run(float tick)
    {
        Resize();

        // 更新时间
        LAppPal.DeltaTime = tick;

        if (RunPre())
        {
            return;
        }

        //更新绘制
        View.Render();

        RunPost();
    }

    /// <summary>
    /// 鼠标按键回调函数（对应 glfwSetMouseButtonCallback）。
    /// </summary>
    /// <param name="button">按键类型</param>
    /// <param name="action">执行结果</param>
    public void OnMouseCallBack(bool press)
    {
        if (press)
        {
            _captured = true;
            View.OnTouchesBegan(_mouseX, _mouseY);
        }
        else
        {
            if (_captured)
            {
                _captured = false;
                View.OnTouchesEnded(_mouseX, _mouseY);
            }
        }
    }

    /// <summary>
    /// 鼠标移动回调函数（对应 glfwSetCursorPosCallback）。
    /// </summary>
    /// <param name="x">x 坐标</param>
    /// <param name="y">y 坐标</param>
    public void OnMouseCallBack(float x, float y)
    {
        if (!_captured)
        {
            return;
        }

        _mouseX = x;
        _mouseY = y;

        View.OnTouchesMoved(_mouseX, _mouseY);
    }
}
