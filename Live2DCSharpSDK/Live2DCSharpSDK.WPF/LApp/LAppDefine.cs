using Live2DCSharpSDK.Framework;

namespace Live2DCSharpSDK.WPF.LApp;

/// <summary>
/// 应用程序类。
/// 管理 Cubism SDK。
/// </summary>
public static class LAppDefine
{
    // 屏幕
    public const float ViewScale = 1.0f;
    public const float ViewMaxScale = 2.0f;
    public const float ViewMinScale = 0.8f;

    public const float ViewLogicalLeft = -1.0f;
    public const float ViewLogicalRight = 1.0f;
    public const float ViewLogicalBottom = -1.0f;
    public const float ViewLogicalTop = -1.0f;

    public const float ViewLogicalMaxLeft = -2.0f;
    public const float ViewLogicalMaxRight = 2.0f;
    public const float ViewLogicalMaxBottom = -2.0f;
    public const float ViewLogicalMaxTop = 2.0f;

    // 与外部定义文件(json)保持一致
    public const string MotionGroupIdle = "Idle"; // 待机
    public const string MotionGroupTapBody = "TapBody"; // 点击身体时

    // 与外部定义文件(json)保持一致
    public const string HitAreaNameHead = "Head";
    public const string HitAreaNameBody = "Body";

    // MOC3 一致性校验选项
    public const bool MocConsistencyValidationEnable = true;

    // Framework 输出的日志级别设定
    public const LogLevel CubismLoggingLevel = LogLevel.Verbose;
}
