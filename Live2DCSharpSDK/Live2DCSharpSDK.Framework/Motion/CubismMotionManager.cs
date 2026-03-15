using Live2DCSharpSDK.Framework.Model;

namespace Live2DCSharpSDK.Framework.Motion;

/// <summary>
/// 动作管理类。
/// </summary>
public class CubismMotionManager : CubismMotionQueueManager
{
    /// <summary>
    /// 当前正在播放的动作的优先级
    /// </summary>
    public MotionPriority CurrentPriority { get; private set; }
    /// <summary>
    /// 即将播放的动作的优先级，播放中为 0。在其他线程读取动作文件时使用的功能。
    /// </summary>
    public MotionPriority ReservePriority { get; set; }

    /// <summary>
    /// 按优先级启动动作。
    /// </summary>
    /// <param name="motion">动作</param>
    /// <param name="autoDelete">播放结束后是否删除动作实例</param>
    /// <param name="priority">优先级</param>
    /// <returns>返回已启动动作的标识编号。无法启动时返回 "-1"。</returns>
    public CubismMotionQueueEntry StartMotionPriority(ACubismMotion motion, MotionPriority priority)
    {
        if (priority == ReservePriority)
        {
            ReservePriority = 0;           // 取消预约
        }

        CurrentPriority = priority;        // 设置正在播放的动作的优先级

        return StartMotion(motion);
    }

    /// <summary>
    /// 更新动作并将参数往应用到模型。
    /// </summary>
    /// <param name="model">目标模型</param>
    /// <param name="deltaTimeSeconds">居陷时间[秒]</param>
    /// <returns>true    已更新
    /// false   未更新</returns>
    public bool UpdateMotion(CubismModel model, float deltaTimeSeconds)
    {
        UserTimeSeconds += deltaTimeSeconds;

        bool updated = DoUpdateMotion(model, UserTimeSeconds);

        if (IsFinished())
        {
            CurrentPriority = 0;           // 清除正在播放动作的优先级
        }

        return updated;
    }

    /// <summary>
    /// 预约动作。
    /// </summary>
    /// <param name="priority">优先级</param>
    /// <returns>true    预约成功
    /// false   预约失败</returns>
    public bool ReserveMotion(MotionPriority priority)
    {
        if ((priority <= ReservePriority) || (priority <= CurrentPriority))
        {
            return false;
        }

        ReservePriority = priority;

        return true;
    }
}
