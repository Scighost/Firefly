using Live2DCSharpSDK.Framework.Effect;
using Live2DCSharpSDK.Framework.Math;
using Live2DCSharpSDK.Framework.Motion;
using Live2DCSharpSDK.Framework.Physics;
using Live2DCSharpSDK.Framework.Rendering;

namespace Live2DCSharpSDK.Framework.Model;

/// <summary>
/// 用户实际使用的模型基类，由用户继承并实现。
/// </summary>
public abstract class CubismUserModel : IDisposable
{
    /// <summary>
    /// 渲染器
    /// </summary>
    public CubismRenderer? Renderer { get; protected set; }
    /// <summary>
    /// 模型矩阵
    /// </summary>
    public CubismModelMatrix ModelMatrix { get; protected set; }
    /// <summary>
    /// Model 实例
    /// </summary>
    public CubismModel Model => _moc.Model;
    /// <summary>
    /// 是否已初始化
    /// </summary>
    public bool Initialized { get; set; }
    /// <summary>
    /// 是否正在更新
    /// </summary>
    public bool Updating { get; set; }
    /// <summary>
    /// 不透明度
    /// </summary>
    public float Opacity { get; set; }

    /// <summary>
    /// Moc 数据
    /// </summary>
    public CubismMoc _moc;

    /// <summary>
    /// 动作管理器
    /// </summary>
    public CubismMotionManager _motionManager;
    /// <summary>
    /// 表情管理器
    /// </summary>
    public CubismExpressionMotionManager _expressionManager;
    /// <summary>
    /// 自动眼驱
    /// </summary>
    public CubismEyeBlink? _eyeBlink;
    /// <summary>
    /// 呼吸
    /// </summary>
    public CubismBreath _breath;
    /// <summary>
    /// 姿势管理器
    /// </summary>
    public CubismPose? _pose;
    /// <summary>
    /// 鼠标拖拽目标点
    /// </summary>
    public CubismTargetPoint _dragManager;
    /// <summary>
    /// 物理演算
    /// </summary>
    public CubismPhysics? _physics;
    /// <summary>
    /// 用户数据
    /// </summary>
    public CubismModelUserData? _modelUserData;
    /// <summary>
    /// 是否进行口型同步
    /// </summary>
    public bool _lipSync;
    /// <summary>
    /// 最后一次口型同步的控制値
    /// </summary>
    public float _lastLipSyncValue;
    /// <summary>
    /// 鼠标拖拽的 X 位置
    /// </summary>
    public float _dragX;
    /// <summary>
    /// 鼠标拖拽的 Y 位置
    /// </summary>
    public float _dragY;
    /// <summary>
    /// X 轴方向的加速度
    /// </summary>
    public float _accelerationX;
    /// <summary>
    /// Y 轴方向的加速度
    /// </summary>
    public float _accelerationY;
    /// <summary>
    /// Z 轴方向的加速度
    /// </summary>
    public float _accelerationZ;
    /// <summary>
    /// 是否校验 MOC3 数据一致性
    /// </summary>
    public bool _mocConsistency;

    /// <summary>
    /// 注册到 CubismMotionQueueManager 的事件回调，调用 CubismUserModel 继承类中的 EventFired 方法。
    /// </summary>
    /// <param name="eventValue">触发的事件字符串数据</param>
    /// <param name="customData">预期为继承 CubismUserModel 的实例</param>
    public static void CubismDefaultMotionEventCallback(CubismUserModel? customData, string eventValue)
    {
        customData?.MotionEventFired(eventValue);
    }

    /// <summary>
    /// 构造函数。
    /// </summary>
    public CubismUserModel()
    {
        _lipSync = true;

        Opacity = 1.0f;

        // 创建动作管理器
        // 继承自 MotionQueueManager 类，使用方式相同
        _motionManager = new();
        _motionManager.SetEventCallback(CubismDefaultMotionEventCallback, this);

        // 创建表情动作管理器
        _expressionManager = new();

        // 拖拽响应动画
        _dragManager = new();
    }

    public void Dispose()
    {
        _moc.Dispose();

        DeleteRenderer();
    }

    /// <summary>
    /// 设置鼠标拖拽信息。
    /// </summary>
    /// <param name="x">拖拽光标的 X 位置</param>
    /// <param name="y">拖拽光标的 Y 位置</param>
    public void SetDragging(float x, float y)
    {
        _dragManager.Set(x, y);
    }

    /// <summary>
    /// 设置加速度信息。
    /// </summary>
    /// <param name="x">X 轴方向的加速度</param>
    /// <param name="y">Y 轴方向的加速度</param>
    /// <param name="z">Z 轴方向的加速度</param>
    protected void SetAcceleration(float x, float y, float z)
    {
        _accelerationX = x;
        _accelerationY = y;
        _accelerationZ = z;
    }

    /// <summary>
    /// 读取模型数据。
    /// </summary>
    /// <param name="buffer">已加载 moc3 文件的缓冲区</param>
    /// <param name="shouldCheckMocConsistency">MOC 数据一致性校验标志（默认値：false）</param>
    protected void LoadModel(byte[] buffer, bool shouldCheckMocConsistency = false)
    {
        _moc = new CubismMoc(buffer, shouldCheckMocConsistency);
        Model.SaveParameters();
        ModelMatrix = new CubismModelMatrix(Model.GetCanvasWidth(), Model.GetCanvasHeight());
    }

    /// <summary>
    /// 读取姿势数据。
    /// </summary>
    /// <param name="buffer">已加载 pose3.json 的缓冲区</param>
    protected void LoadPose(string buffer)
    {
        _pose = new CubismPose(buffer);
    }

    /// <summary>
    /// 读取物理演算数据。
    /// </summary>
    /// <param name="buffer">已加载 physics3.json 的缓冲区</param>
    protected void LoadPhysics(string buffer)
    {
        _physics = new CubismPhysics(buffer);
    }

    /// <summary>
    /// 读取用户数据。
    /// </summary>
    /// <param name="buffer">已加载 userdata3.json 的缓冲区</param>
    protected void LoadUserData(string buffer)
    {
        _modelUserData = new CubismModelUserData(buffer);
    }

    /// <summary>
    /// 获取指定位置是否命中 Drawable。
    /// </summary>
    /// <param name="drawableId">要检测的 Drawable 的 ID</param>
    /// <param name="pointX">X 位置</param>
    /// <param name="pointY">Y 位置</param>
    /// <returns>true    命中
    /// false   未命中</returns>
    public unsafe bool IsHit(string drawableId, float pointX, float pointY)
    {
        var drawIndex = Model.GetDrawableIndex(drawableId);

        if (drawIndex < 0)
        {
            return false; // 不存在则返回 false
        }

        var count = Model.GetDrawableVertexCount(drawIndex);
        var vertices = Model.GetDrawableVertices(drawIndex);

        var left = vertices[0];
        var right = vertices[0];
        var top = vertices[1];
        var bottom = vertices[1];

        for (int j = 1; j < count; ++j)
        {
            var x = vertices[CubismFramework.VertexOffset + j * CubismFramework.VertexStep];
            var y = vertices[CubismFramework.VertexOffset + j * CubismFramework.VertexStep + 1];

            if (x < left)
            {
                left = x; // Min x
            }

            if (x > right)
            {
                right = x; // Max x
            }

            if (y < top)
            {
                top = y; // Min y
            }

            if (y > bottom)
            {
                bottom = y; // Max y
            }
        }

        var tx = ModelMatrix.InvertTransformX(pointX);
        var ty = ModelMatrix.InvertTransformY(pointY);

        return (left <= tx) && (tx <= right) && (top <= ty) && (ty <= bottom);
    }

    /// <summary>
    /// 生成渲染器并执行初始化。
    /// </summary>
    protected void CreateRenderer(CubismRenderer renderer)
    {
        Renderer = renderer;
    }

    /// <summary>
    /// 释放渲染器。
    /// </summary>
    protected void DeleteRenderer()
    {
        if (Renderer != null)
        {
            Renderer.Dispose();
            Renderer = null;
        }
    }

    /// <summary>
    /// 当动作播放时触发事件时进行处理。
    /// 预期通过继承来重写此方法。
    /// 若不重写，则仅输出日志。
    /// </summary>
    /// <param name="eventValue">触发的事件字符串数据</param>
    protected abstract void MotionEventFired(string eventValue);
}
