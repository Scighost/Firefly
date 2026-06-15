namespace Live2DCSharpSDK.Framework.Math;

/// <summary>
/// 提供面部朝向控制功能的类。
/// 使用临界阻尼弹簧模型，保证无振荡收敛。
/// </summary>
public class CubismTargetPoint
{
    public const float Epsilon = 0.001f;

    /// <summary>
    /// 面部朝向 X (-1.0 - 1.0)
    /// </summary>
    public float FaceX { get; private set; }
    /// <summary>
    /// 面部朝向 Y (-1.0 - 1.0)
    /// </summary>
    public float FaceY { get; private set; }

    /// <summary>
    /// 弹簧响应时间（秒），值越小响应越快。默认 0.15。
    /// </summary>
    public float SmoothTime { get; set; } = 0.15f;

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
    /// 执行更新处理。
    /// </summary>
    /// <param name="deltaTimeSeconds">增量时间[秒]</param>
    public void Update(float deltaTimeSeconds)
    {
        if (deltaTimeSeconds <= 0.0f)
        {
            return;
        }

        // Clamp dt to avoid instability on large pauses
        float dt = MathF.Min(deltaTimeSeconds, 0.1f);

        float dx = _faceTargetX - FaceX;
        float dy = _faceTargetY - FaceY;

        if (MathF.Abs(dx) <= Epsilon && MathF.Abs(dy) <= Epsilon
            && MathF.Abs(_faceVX) <= Epsilon && MathF.Abs(_faceVY) <= Epsilon)
        {
            FaceX = _faceTargetX;
            FaceY = _faceTargetY;
            _faceVX = 0;
            _faceVY = 0;
            return;
        }

        // Critically damped spring: ζ = 1, ω = 1 / SmoothTime
        // acceleration = -ω² * displacement - 2 * ω * velocity
        float st = MathF.Max(SmoothTime, 0.01f);
        float omega = 1.0f / st;
        float omega2 = omega * omega;
        float twoOmega = 2.0f * omega;

        // Semi-implicit Euler: update velocity first, then position
        _faceVX += (omega2 * dx - twoOmega * _faceVX) * dt;
        _faceVY += (omega2 * dy - twoOmega * _faceVY) * dt;

        FaceX += _faceVX * dt;
        FaceY += _faceVY * dt;
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
