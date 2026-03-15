namespace Live2DCSharpSDK.Framework.Math;

/// <summary>
/// 提供面部朝向控制功能的类。
/// </summary>
public class CubismTargetPoint
{
    public const int FrameRate = 30;
    public const float Epsilon = 0.01f;

    /// <summary>
    /// 面部朝向 X (-1.0 - 1.0)
    /// </summary>
    public float FaceX { get; private set; }
    /// <summary>
    /// 面部朝向 Y (-1.0 - 1.0)
    /// </summary>
    public float FaceY { get; private set; }

    /// <summary>
    /// 面部朝向的 X 目标值（会接近此值）
    /// </summary>
    private float _faceTargetX;
    /// <summary>
    /// 面部朝向的 Y 目标值（会接近此值）
    /// </summary>
    private float _faceTargetY;
    /// <summary>
    /// 面部朝向变化速度 X
    /// </summary>
    private float _faceVX;
    /// <summary>
    /// 面部朝向变化速度 Y
    /// </summary>
    private float _faceVY;
    /// <summary>
    /// 上次执行时间[秒]
    /// </summary>
    private float _lastTimeSeconds;
    /// <summary>
    /// 累积的增量时间[秒]
    /// </summary>
    private float _userTimeSeconds;

    /// <summary>
    /// 执行更新处理。
    /// </summary>
    /// <param name="deltaTimeSeconds">增量时间[秒]</param>
    public void Update(float deltaTimeSeconds)
    {
        // 累加增量时间
        _userTimeSeconds += deltaTimeSeconds;

        // 头部从中心向左右摆动的平均时间约为若干秒。
        // 考虑到加速/减速，将其两倍作为最高速度。
        // 将面部朝向范围设为中心(0.0)，左右为(±1.0)
        float FaceParamMaxV = 40.0f / 10.0f;                                      // 在约7.5秒内移动40单位（约5.3/秒）
        float MaxV = FaceParamMaxV * 1.0f / FrameRate;  // 每帧可变化的速度上限

        if (_lastTimeSeconds == 0.0f)
        {
            _lastTimeSeconds = _userTimeSeconds;
            return;
        }

        float deltaTimeWeight = (_userTimeSeconds - _lastTimeSeconds) * FrameRate;
        _lastTimeSeconds = _userTimeSeconds;

        // 达到最高速度所需的时间
        float TimeToMaxSpeed = 0.15f;
        float FrameToMaxSpeed = TimeToMaxSpeed * FrameRate;     // sec * frame/sec
        float MaxA = deltaTimeWeight * MaxV / FrameToMaxSpeed;                           // 每帧的加速度

        // 目标朝向为 (dx, dy) 方向的向量
        float dx = _faceTargetX - FaceX;
        float dy = _faceTargetY - FaceY;

        if (MathF.Abs(dx) <= Epsilon && MathF.Abs(dy) <= Epsilon)
        {
            return; // 无变化
        }

        // 若大于最大速度则降低速度
        float d = MathF.Sqrt((dx * dx) + (dy * dy));

        // 进方向的最大速度向量
        float vx = MaxV * dx / d;
        float vy = MaxV * dy / d;

        // 从当前速度计算到目标速度的变化（加速度）
        float ax = vx - _faceVX;
        float ay = vy - _faceVY;

        float a = MathF.Sqrt((ax * ax) + (ay * ay));

        // 加速时
        if (a < -MaxA || a > MaxA)
        {
            ax *= MaxA / a;
            ay *= MaxA / a;
        }

        // 将加速度加到原速度上得到新速度
        _faceVX += ax;
        _faceVY += ay;

        // 接近目标方向时为平滑减速的处理
        // 根据加速度、速度与距离的关系计算当前可达到的最高速度，超过时进行降速
        // ※真实人体可以通过肌力调整加速度，处理上做了简化
        {
            // 加速度、速度、距离之间的关系式。
            // （表达式略，t=1 时进行简化）

            float maxV = 0.5f * (MathF.Sqrt((MaxA * MaxA) + 16.0f * MaxA * d - 8.0f * MaxA * d) - MaxA);
            float curV = MathF.Sqrt((_faceVX * _faceVX) + (_faceVY * _faceVY));

            if (curV > maxV)
            {
                // 当当前速度 > 最高速度时，减速到最高速度
                _faceVX *= maxV / curV;
                _faceVY *= maxV / curV;
            }
        }

        FaceX += _faceVX;
        FaceY += _faceVY;
    }

    /// <summary>
    /// 设置面部朝向的目标值。
    /// </summary>
    /// <param name="x">X 轴的面部朝向值 (-1.0 - 1.0)</param>
    /// <param name="y">Y 轴的面部朝向值 (-1.0 - 1.0)</param>
    public void Set(float x, float y)
    {
        _faceTargetX = x;
        _faceTargetY = y;
    }
}
