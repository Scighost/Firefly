namespace Live2DCSharpSDK.Framework.Effect;

/// <summary>
/// 呼吸参数信息。
/// </summary>
public record BreathParameterData
{
    /// <summary>
    /// 绑定呼吸的参数 ID
    /// </summary>
    public required string ParameterId { get; set; }
    /// <summary>
    /// 将呼吸视为正弦波时的波形偏移
    /// </summary>
    public float Offset { get; set; }
    /// <summary>
    /// 将呼吸视为正弦波时的波形幅度
    /// </summary>
    public float Peak { get; set; }
    /// <summary>
    /// 将呼吸视为正弦波时的波形周期
    /// </summary>
    public float Cycle { get; set; }
    /// <summary>
    /// 参数的权重
    /// </summary>
    public float Weight { get; set; }
}