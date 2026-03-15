using Live2DCSharpSDK.Framework.Model;

namespace Live2DCSharpSDK.Framework.Motion;

/// <summary>
/// 动作播放管理类。用于播放 CubismMotion 等 ACubismMotion 子类的动作。
/// 
/// 播放中如果调用 StartMotion()，将平滚Ble 切换到新动作，旧动作中断。
/// 若需同时播放多个动作（如表情动作、身体动作等），请使用多个 CubismMotionQueueManager 实例。
/// </summary>
public class CubismMotionQueueManager
{
    /// <summary>
    /// 动作列表
    /// </summary>
    protected readonly List<CubismMotionQueueEntry> Motions = [];

    private readonly List<CubismMotionQueueEntry> _remove = [];

    /// <summary>
    /// 回调函数指针
    /// </summary>
    private CubismMotionEventFunction? _eventCallback;
    /// <summary>
    /// 返回到回调的数据
    /// </summary>
    private CubismUserModel? _eventCustomData;

    /// <summary>
    /// 居陷时间的累计値[秒]
    /// </summary>
    protected float UserTimeSeconds;

    /// <summary>
    /// 启动指定动作。若同类型动作已存在，则向已有动作置位结束标志并开始淡出。
    /// </summary>
    /// <param name="motion">要启动的动作</param>
    /// <returns>返回已启动动作的标识编号，用于 IsFinished() 判断参数。无法启动时返回 "-1"。</returns>
    public CubismMotionQueueEntry StartMotion(ACubismMotion motion)
    {
        CubismMotionQueueEntry motionQueueEntry;

        // 如果已有动作，置位结束标志
        for (int i = 0; i < Motions.Count; ++i)
        {
            motionQueueEntry = Motions[i];
            if (motionQueueEntry == null)
            {
                continue;
            }

            motionQueueEntry.SetFadeout(motionQueueEntry.Motion.FadeOutSeconds);
        }

        motionQueueEntry = new CubismMotionQueueEntry()
        {
            Motion = motion
        };

        Motions.Add(motionQueueEntry);

        return motionQueueEntry;
    }

    /// <summary>
    /// 启动指定动作。若同类型动作已存在，则向已有动作置位结束标志并开始淡出。
    /// </summary>
    /// <param name="motion">要启动的动作</param>
    /// <param name="autoDelete">播放结束后是否删除动作实例</param>
    /// <param name="userTimeSeconds">居陷时间累计値[秒]</param>
    /// <returns>返回已启动动作的标识编号。无法启动时返回 "-1"。</returns>
    [Obsolete("Please use StartMotion(ACubismMotion motion")]
    public CubismMotionQueueEntry StartMotion(ACubismMotion motion, float userTimeSeconds)
    {
        CubismLog.Warning("[Live2D SDK] StartMotion(ACubismMotion motion, float userTimeSeconds) is a deprecated function. Please use StartMotion(ACubismMotion motion).");

        CubismMotionQueueEntry motionQueueEntry;

        // 如果已有动作，置位结束标志
        for (int i = 0; i < Motions.Count; ++i)
        {
            motionQueueEntry = Motions[i];
            if (motionQueueEntry == null)
            {
                continue;
            }

            motionQueueEntry.SetFadeout(motionQueueEntry.Motion.FadeOutSeconds);
        }

        motionQueueEntry = new CubismMotionQueueEntry
        {
            Motion = motion
        }; // 结束时销毁

        Motions.Add(motionQueueEntry);

        return motionQueueEntry;
    }

    /// <summary>
    /// 所有动作是否已全部结束。
    /// </summary>
    /// <returns>true    全部已结束
    /// false   尚未结束</returns>
    public bool IsFinished()
    {
        // ------- 执行处理 --------
        // 如果已有动作，置位结束标志
        for (int i = 0; i < Motions.Count; i++)
        {
            // ----- 如果已完成的则将其删除 ------
            if (!Motions[i].Finished)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 检查指定动作是否已结束。
    /// </summary>
    /// <param name="motionQueueEntryNumber">动作的标识编号</param>
    /// <returns>true    指定动作已结束
    /// false   尚未结束</returns>
    public bool IsFinished(object motionQueueEntryNumber)
    {
        // 如果已有动作，置位结束标志

        for (int i = 0; i < Motions.Count; i++)
        {
            CubismMotionQueueEntry? item = Motions[i];
            if (item == null)
            {
                continue;
            }

            if (item == motionQueueEntryNumber && !item.Finished)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 停止所有动作。
    /// </summary>
    public void StopAllMotions()
    {
        // ------- 执行处理 --------
        // 如果已有动作，置位结束标志

        Motions.Clear();
    }

    /// <summary>
    /// 获取指定的 CubismMotionQueueEntry。
    /// </summary>
    /// <param name="motionQueueEntryNumber">动作的标识编号</param>
    /// <returns>指定的 CubismMotionQueueEntry 指针，未找到则返回 NULL</returns>
    public CubismMotionQueueEntry? GetCubismMotionQueueEntry(object motionQueueEntryNumber)
    {
        //------- 执行处理 --------
        //如果已有动作，置位结束标志

        for (int i = 0; i < Motions.Count; i++)
        {
            if (Motions[i] == motionQueueEntryNumber)
            {
                return Motions[i];
            }
        }

        return null;
    }

    /// <summary>
    /// 注册接收事件的回调。
    /// </summary>
    /// <param name="callback">回调函数</param>
    /// <param name="customData">返回给回调的数据</param>
    public void SetEventCallback(CubismMotionEventFunction callback, CubismUserModel customData)
    {
        _eventCallback = callback;
        _eventCustomData = customData;
    }

    /// <summary>
    /// 更新动作并将参数往应用到模型。
    /// </summary>
    /// <param name="model">目标模型</param>
    /// <param name="userTimeSeconds">居陷时间累计値[秒]</param>
    /// <returns>true    已将参数往应用到模型
    /// false   未应用（动作无变化）</returns>
    public virtual bool DoUpdateMotion(CubismModel model, float userTimeSeconds)
    {
        bool updated = false;

        // ------- 执行处理 --------
        // 如果已有动作，置位结束标志

        _remove.Clear();

        for (int i1 = 0; i1 < Motions.Count; i1++)
        {
            CubismMotionQueueEntry? item = Motions[i1];
            var motion = item.Motion;

            // ------ 将冗应用倇 ------
            motion.UpdateParameters(model, item, userTimeSeconds);
            updated = true;

            // ------ 检查用户触发事件 ----
            var firedList = motion.GetFiredEvent(
                item.LastEventCheckSeconds - item.StartTime,
                userTimeSeconds - item.StartTime);

            for (int i = 0; i < firedList.Count; ++i)
            {
                _eventCallback?.Invoke(_eventCustomData, firedList[i]);
            }

            item.LastEventCheckSeconds = userTimeSeconds;

            // ----- 如果已完成则将其删除 ------
            if (item.Finished)
            {
                _remove.Add(item);          // 删除
            }
            else
            {
                if (item.IsTriggeredFadeOut)
                {
                    item.StartFadeout(item.FadeOutSeconds, userTimeSeconds);
                }
            }
        }

        foreach (var item in _remove)
        {
            Motions.Remove(item);
        }

        return updated;
    }
}
