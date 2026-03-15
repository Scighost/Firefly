using Live2DCSharpSDK.Framework.Model;

namespace Live2DCSharpSDK.Framework.Motion;

/// <summary>
/// 动作播放结束回调函数定义
/// </summary>
public delegate void FinishedMotionCallback(CubismModel model, ACubismMotion self);

/// <summary>
/// 可注册为事件回调的函数的类型信息
/// </summary>
/// <param name="eventValue">触发的事件字符串数据</param>
/// <param name="customData">回调时返回的注册时指定的数据</param>
public delegate void CubismMotionEventFunction(CubismUserModel? customData, string eventValue);

/// <summary>
/// 动作曲线线段的评估函数。
/// </summary>
/// <param name="points">动作曲线的控制点列表</param>
/// <param name="time">评估时间[秒]</param>
public delegate float csmMotionSegmentEvaluationFunction(CubismMotionPoint[] points, int start, float time);

/// <summary>
/// 动作优先级常量
/// </summary>
public enum MotionPriority : int
{
    PriorityNone = 0,
    PriorityIdle = 1,
    PriorityNormal = 2,
    PriorityForce = 3
}

/// <summary>
/// 表情参数倗计算方式
/// </summary>
public enum ExpressionBlendType
{
    /// <summary>
    /// 加算
    /// </summary>
    Add = 0,
    /// <summary>
    /// 乘算
    /// </summary>
    Multiply = 1,
    /// <summary>
    /// 覆盖写入
    /// </summary>
    Overwrite = 2
};

/// <summary>
/// 表情参数信息的结构体。
/// </summary>
public record ExpressionParameter
{
    /// <summary>
    /// 参数 ID
    /// </summary>
    public required string ParameterId { get; set; }
    /// <summary>
    /// 参数的运算类型
    /// </summary>
    public ExpressionBlendType BlendType { get; set; }
    /// <summary>
    /// 値
    /// </summary>
    public float Value { get; set; }
}


/// <summary>
/// 动作曲线的目标类型。
/// </summary>
public enum CubismMotionCurveTarget
{
    /// <summary>
    /// 针对模型
    /// </summary>
    Model,
    /// <summary>
    /// 针对参数
    /// </summary>
    Parameter,
    /// <summary>
    /// 针对部件不透明度
    /// </summary>
    PartOpacity
};

/// <summary>
/// 动作曲线线段的类型。
/// </summary>
public enum CubismMotionSegmentType : int
{
    /// <summary>
    /// 线性
    /// </summary>
    Linear = 0,
    /// <summary>
    /// 趝塞尔曲线
    /// </summary>
    Bezier = 1,
    /// <summary>
    /// 阶梯
    /// </summary>
    Stepped = 2,
    /// <summary>
    /// 逆阶梯
    /// </summary>
    InverseStepped = 3
};

/// <summary>
/// 动作曲线的控制点。
/// </summary>
public struct CubismMotionPoint
{
    /// <summary>
    /// 时间[秒]
    /// </summary>
    public float Time;
    /// <summary>
    /// 假
    /// </summary>
    public float Value;
}

/// <summary>
/// 动作曲线线段。
/// </summary>
public record CubismMotionSegment
{
    /// <summary>
    /// 使用的评估函数
    /// </summary>
    public csmMotionSegmentEvaluationFunction Evaluate;
    /// <summary>
    /// 第一个线段的索引
    /// </summary>
    public int BasePointIndex;
    /// <summary>
    /// 线段类型
    /// </summary>
    public CubismMotionSegmentType SegmentType;
}

/// <summary>
/// 动作曲线。
/// </summary>
public record CubismMotionCurve
{
    /// <summary>
    /// 曲线类型
    /// </summary>
    public CubismMotionCurveTarget Type;
    /// <summary>
    /// 曲线 ID
    /// </summary>
    public string Id;
    /// <summary>
    /// 线段数量
    /// </summary>
    public int SegmentCount;
    /// <summary>
    /// 第一个线段的索引
    /// </summary>
    public int BaseSegmentIndex;
    /// <summary>
    /// 淡入所需时间[秒]
    /// </summary>
    public float FadeInTime;
    /// <summary>
    /// 淡出所需时间[秒]
    /// </summary>
    public float FadeOutTime;
}

/// <summary>
/// 事件。
/// </summary>
public record CubismMotionEvent
{
    public float FireTime;
    public string Value;
}

/// <summary>
/// 动作数据。
/// </summary>
public record CubismMotionData
{
    /// <summary>
    /// 动作长度[秒]
    /// </summary>
    public float Duration;
    /// <summary>
    /// 是否循环
    /// </summary>
    public bool Loop;
    /// <summary>
    /// 曲线数量
    /// </summary>
    public int CurveCount;
    /// <summary>
    /// UserData 数量
    /// </summary>
    public int EventCount;
    /// <summary>
    /// 帧率
    /// </summary>
    public float Fps;
    /// <summary>
    /// 曲线列表
    /// </summary>
    public CubismMotionCurve[] Curves;
    /// <summary>
    /// 线段列表
    /// </summary>
    public CubismMotionSegment[] Segments;
    /// <summary>
    /// 控制点列表
    /// </summary>
    public CubismMotionPoint[] Points;
    /// <summary>
    /// 事件列表
    /// </summary>
    public CubismMotionEvent[] Events;
}