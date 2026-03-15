namespace Live2DCSharpSDK.Framework.Effect;

public enum EyeState
{
    /// <summary>
    /// 初始状态
    /// </summary>
    First = 0,
    /// <summary>
    /// 未处于眨眼的状态
    /// </summary>
    Interval,
    /// <summary>
    /// 眼睑正在闭合的状态
    /// </summary>
    Closing,
    /// <summary>
    /// 眼睑已闭合的状态
    /// </summary>
    Closed,
    /// <summary>
    /// 眼睑正在张开的状态
    /// </summary>
    Opening
};
