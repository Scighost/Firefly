using System.Numerics;

namespace Live2DCSharpSDK.Framework.Physics;

/// <summary>
/// 物理计算应用目标的类型。
/// </summary>
public enum CubismPhysicsTargetType
{
    /// <summary>
    /// 应用于参数
    /// </summary>
    CubismPhysicsTargetType_Parameter,
}

/// <summary>
/// 物理计算输入的类型。
/// </summary>
public enum CubismPhysicsSource
{
    /// <summary>
    /// 来自 X 轴位置
    /// </summary>
    CubismPhysicsSource_X,
    /// <summary>
    /// 来自 Y 轴位置
    /// </summary>
    CubismPhysicsSource_Y,
    /// <summary>
    /// 来自角度
    /// </summary>
    CubismPhysicsSource_Angle,
}

/// <summary>
/// 物理计算中使用的外力。
/// </summary>
public record PhysicsJsonEffectiveForces
{
    /// <summary>
    /// 重力
    /// </summary>
    public Vector2 Gravity;
    /// <summary>
    /// 风
    /// </summary>
    public Vector2 Wind;
};

/// <summary>
/// 物理计算的参数信息。
/// </summary>
public record CubismPhysicsParameter
{
    /// <summary>
    /// 参数 ID
    /// </summary>
    public required string Id;
    /// <summary>
    /// 应用目标的类型
    /// </summary>
    public CubismPhysicsTargetType TargetType;
};

/// <summary>
/// 物理计算的归一化信息。
/// </summary>
public record CubismPhysicsNormalization
{
    /// <summary>
    /// 最大值
    /// </summary>
    public float Minimum;
    /// <summary>
    /// 最小值
    /// </summary>
    public float Maximum;
    /// <summary>
    /// 默认值
    /// </summary>
    public float Default;
}

/// <summary>
/// 物理计算中使用的物理点信息。
/// </summary>
public record CubismPhysicsParticle
{
    /// <summary>
    /// 初始位置
    /// </summary>
    public Vector2 InitialPosition;
    /// <summary>
    /// 可动性
    /// </summary>
    public float Mobility;
    /// <summary>
    /// 延迟
    /// </summary>
    public float Delay;
    /// <summary>
    /// 加速度
    /// </summary>
    public float Acceleration;
    /// <summary>
    /// 距离
    /// </summary>
    public float Radius;
    /// <summary>
    /// 当前位置
    /// </summary>
    public Vector2 Position;
    /// <summary>
    /// 上一次位置
    /// </summary>
    public Vector2 LastPosition;
    /// <summary>
    /// 上一次重力
    /// </summary>
    public Vector2 LastGravity;
    /// <summary>
    /// 当前受到的力
    /// </summary>
    public Vector2 Force;
    /// <summary>
    /// 当前速度
    /// </summary>
    public Vector2 Velocity;
}

/// <summary>
/// 物理计算中物理点的管理。
/// </summary>
public record CubismPhysicsSubRig
{
    /// <summary>
    /// 输入数量
    /// </summary>
    public int InputCount;
    /// <summary>
    /// 输出数量
    /// </summary>
    public int OutputCount;
    /// <summary>
    /// 物理点数量
    /// </summary>
    public int ParticleCount;
    /// <summary>
    /// 输入的起始索引
    /// </summary>
    public int BaseInputIndex;
    /// <summary>
    /// 输出的起始索引
    /// </summary>
    public int BaseOutputIndex;
    /// <summary>
    /// 物理点的起始索引
    /// </summary>
    public int BaseParticleIndex;
    /// <summary>
    /// 归一化后的位置
    /// </summary>
    public required CubismPhysicsNormalization NormalizationPosition;
    /// <summary>
    /// 归一化后的角度
    /// </summary>
    public required CubismPhysicsNormalization NormalizationAngle;
}

/// <summary>
/// 获取归一化参数值的委托声明。
/// </summary>
/// <param name="targetTranslation">运算结果的位移值</param>
/// <param name="targetAngle">运算结果的角度</param>
/// <param name="value">参数值</param>
/// <param name="parameterMinimumValue">参数最小值</param>
/// <param name="parameterMaximumValue">参数最大值</param>
/// <param name="parameterDefaultValue">参数默认值</param>
/// <param name="normalizationPosition">归一化后的位置</param>
/// <param name="normalizationAngle">归一化后的角度</param>
/// <param name="isInverted">值是否被反转？</param>
/// <param name="weight">权重</param>
public delegate void NormalizedPhysicsParameterValueGetter(
    ref Vector2 targetTranslation,
    ref float targetAngle,
    float value,
    float parameterMinimumValue,
    float parameterMaximumValue,
    float parameterDefaultValue,
    CubismPhysicsNormalization normalizationPosition,
    CubismPhysicsNormalization normalizationAngle,
    bool isInverted,
    float weight
);

/// <summary>
/// 获取物理计算值的委托声明。
/// </summary>
/// <param name="translation">位移值</param>
/// <param name="particles">物理点列表</param>
/// <param name="particleIndex"></param>
/// <param name="isInverted">值是否被反转？</param>
/// <param name="parentGravity">重力</param>
/// <returns>值</returns>
public unsafe delegate float PhysicsValueGetter(
    Vector2 translation,
    CubismPhysicsParticle[] particles,
    int currentParticleIndex,
    int particleIndex,
    bool isInverted,
    Vector2 parentGravity
);

/// <summary>
/// 获取物理计算缩放值的委托声明。
/// </summary>
/// <param name="translationScale">位移值的缩放比例</param>
/// <param name="angleScale">角度的缩放比例</param>
/// <returns>缩放值</returns>
public unsafe delegate float PhysicsScaleGetter(Vector2 translationScale, float angleScale);

/// <summary>
/// 物理计算的输入信息。
/// </summary>
public record CubismPhysicsInput
{
    /// <summary>
    /// 输入来源参数
    /// </summary>
    public required CubismPhysicsParameter Source;
    /// <summary>
    /// 输入来源参数的索引
    /// </summary>
    public int SourceParameterIndex;
    /// <summary>
    /// 权重
    /// </summary>
    public float Weight;
    /// <summary>
    /// 输入类型
    /// </summary>
    public CubismPhysicsSource Type;
    /// <summary>
    /// 值是否被反转
    /// </summary>
    public bool Reflect;
    /// <summary>
    /// 获取归一化参数值的函数
    /// </summary>
    public NormalizedPhysicsParameterValueGetter GetNormalizedParameterValue;
}

/// <summary>
/// 物理计算的输出信息。
/// </summary>
public record CubismPhysicsOutput
{
    /// <summary>
    /// 输出目标参数
    /// </summary>
    public required CubismPhysicsParameter Destination;
    /// <summary>
    /// 输出目标参数的索引
    /// </summary>
    public int DestinationParameterIndex;
    /// <summary>
    /// 钟摆的索引
    /// </summary>
    public int VertexIndex;
    /// <summary>
    /// 位移值的缩放比例
    /// </summary>
    public Vector2 TranslationScale;
    /// <summary>
    /// 角度的缩放比例
    /// </summary>
    public float AngleScale;
    /// <summary>
    /// 权重
    /// </summary>
    public float Weight;
    /// <summary>
    /// 输出类型
    /// </summary>
    public CubismPhysicsSource Type;
    /// <summary>
    /// 值是否被反转
    /// </summary>
    public bool Reflect;
    /// <summary>
    /// 低于最小值时的值
    /// </summary>
    public float ValueBelowMinimum;
    /// <summary>
    /// 超过最大值时的值
    /// </summary>
    public float ValueExceededMaximum;
    /// <summary>
    /// 获取物理计算值的函数
    /// </summary>
    public PhysicsValueGetter GetValue;
    /// <summary>
    /// 获取物理计算缩放值的函数
    /// </summary>
    public PhysicsScaleGetter GetScale;
}

/// <summary>
/// 物理计算数据。
/// </summary>
public record CubismPhysicsRig
{
    /// <summary>
    /// 物理计算的物理点数量
    /// </summary>
    public int SubRigCount;
    /// <summary>
    /// 物理计算物理点管理列表
    /// </summary>
    public required CubismPhysicsSubRig[] Settings;
    /// <summary>
    /// 物理计算输入列表
    /// </summary>
    public required CubismPhysicsInput[] Inputs;
    /// <summary>
    /// 物理计算输出列表
    /// </summary>
    public required CubismPhysicsOutput[] Outputs;
    /// <summary>
    /// 物理计算物理点列表
    /// </summary>
    public required CubismPhysicsParticle[] Particles;
    /// <summary>
    /// 重力
    /// </summary>
    public Vector2 Gravity;
    /// <summary>
    /// 风
    /// </summary>
    public Vector2 Wind;
    /// <summary>
    /// 物理计算运行 FPS
    /// </summary>
    public float Fps;
}