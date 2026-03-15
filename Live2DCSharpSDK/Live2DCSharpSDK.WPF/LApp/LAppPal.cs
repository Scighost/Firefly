namespace Live2DCSharpSDK.WPF.LApp;

/// <summary>
/// 抽象化平台依赖功能的 Cubism Platform Abstraction Layer。
/// 汇集文件读取、时间获取等平台依赖函数。
/// </summary>
public static class LAppPal
{
    /// <summary>
    /// 获取增量时间（与上一帧的差值）。
    /// </summary>
    /// <returns>增量时间[秒]</returns>
    public static float DeltaTime { get; set; }
}
