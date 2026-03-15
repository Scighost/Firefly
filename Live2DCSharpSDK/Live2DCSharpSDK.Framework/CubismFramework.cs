using Live2DCSharpSDK.Framework.Core;
using Live2DCSharpSDK.Framework.Id;

namespace Live2DCSharpSDK.Framework;

/// <summary>
/// Live2D Cubism Original Workflow SDK 的入口点。
/// 使用时请调用 CubismFramework.Initialize() 开始，调用 CubismFramework.Dispose() 结束。
/// </summary>
public static class CubismFramework
{
    /// <summary>
    /// 网格顶点的偏移值
    /// </summary>
    public const int VertexOffset = 0;
    /// <summary>
    /// 网格顶点的步长值
    /// </summary>
    public const int VertexStep = 2;

    /// <summary>
    /// 获取 ID 管理器的实例。
    /// </summary>
    public static CubismIdManager CubismIdManager { get; private set; } = new();

    public static bool IsStarted { get; private set; }

    private static ICubismAllocator? s_allocator;
    private static CubismOption? s_option;

    /// <summary>
    /// 使 Cubism Framework 的 API 可用。
    /// 在执行 API 之前必须调用此函数。
    /// 请务必在参数中传入内存分配器。
    /// 一旦准备完成，之后再次执行会跳过内部处理。
    /// </summary>
    /// <param name="allocator">ICubismAllocator 类的实例</param>
    /// <param name="option">Option 类的实例</param>
    /// <returns>准备处理完成时返回 true。</returns>
    public static bool StartUp(ICubismAllocator allocator, CubismOption option)
    {
        if (IsStarted)
        {
            CubismLog.Info("[Live2D SDK]CubismFramework.StartUp() is already done.");
            return IsStarted;
        }

        s_option = option;
        if (s_option != null)
        {
            CubismCore.SetLogFunction(s_option.LogFunction);
        }

        if (allocator == null)
        {
            CubismLog.Warning("[Live2D SDK]CubismFramework.StartUp() failed, need allocator instance.");
            IsStarted = false;
        }
        else
        {
            s_allocator = allocator;
            IsStarted = true;
        }

        // 显示 Live2D Cubism Core 版本信息
        if (IsStarted)
        {
            var version = CubismCore.GetVersion();

            uint major = (version & 0xFF000000) >> 24;
            uint minor = (version & 0x00FF0000) >> 16;
            uint patch = version & 0x0000FFFF;
            uint versionNumber = version;

            CubismLog.Info($"[Live2D SDK]Cubism Core version: {major:#0}.{minor:0}.{patch:0000} ({versionNumber})");
        }

        CubismLog.Info("[Live2D SDK]CubismFramework.StartUp() is complete.");

        return IsStarted;
    }

    /// <summary>
    /// 清除通过 StartUp() 初始化的 CubismFramework 的各参数。
    /// 在要重新使用已 Dispose 的 CubismFramework 时使用。
    /// </summary>
    public static void CleanUp()
    {
        IsStarted = false;
    }

    /// <summary>
    /// 执行绑定到 Core API 的日志函数
    /// </summary>
    /// <param name="data">日志消息</param>
    public static void CoreLogFunction(string data)
    {
        CubismCore.GetLogFunction()?.Invoke(data);
    }

    /// <summary>
    /// 返回当前日志输出级别的值。
    /// </summary>
    /// <returns>当前日志输出级别的值</returns>
    public static LogLevel GetLoggingLevel()
    {
        if (s_option != null)
            return s_option.LoggingLevel;

        return LogLevel.Off;
    }

    public static IntPtr Allocate(int size)
        => s_allocator!.Allocate(size);
    public static IntPtr AllocateAligned(int size, int alignment)
        => s_allocator!.AllocateAligned(size, alignment);
    public static void Deallocate(IntPtr address)
        => s_allocator!.Deallocate(address);
    public static void DeallocateAligned(IntPtr address)
        => s_allocator!.DeallocateAligned(address);
}
