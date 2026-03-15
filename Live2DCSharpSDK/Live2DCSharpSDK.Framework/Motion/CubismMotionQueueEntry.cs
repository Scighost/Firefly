namespace Live2DCSharpSDK.Framework.Motion;

/// <summary>
/// CubismMotionQueueManager 中每个动作播放状态的管理类。
/// </summary>
public class CubismMotionQueueEntry
{
    /// <summary>
    /// 动作
    /// </summary>
    public required ACubismMotion Motion { get; set; }

    /// <summary>
    /// 有效标志
    /// </summary>
    public bool Available { get; set; }
    /// <summary>
    /// 结束标志
    /// </summary>
    public bool Finished { get; set; }
    /// <summary>
    /// 开始标志（0.9.00 之后）
    /// </summary>
    public bool Started { get; set; }
    /// <summary>
    /// 动作播放开始时刻[秒]
    /// </summary>
    public float StartTime { get; set; }
    /// <summary>
    /// 淡入开始时刻（循环时仅首次）[秒]
    /// </summary>
    public float FadeInStartTime { get; set; }
    /// <summary>
    /// 预定结束时刻[秒]
    /// </summary>
    public float EndTime { get; set; }
    /// <summary>
    /// 时刻状态[秒]
    /// </summary>
    public float StateTime { get; private set; }
    /// <summary>
    /// 权重状态
    /// </summary>
    public float StateWeight { get; private set; }
    /// <summary>
    /// 动作侧最后一次检查的时间
    /// </summary>
    public float LastEventCheckSeconds { get; set; }

    public float FadeOutSeconds { get; private set; }

    public bool IsTriggeredFadeOut { get; private set; }

    /// <summary>
    /// 构造函数。
    /// </summary>
    public CubismMotionQueueEntry()
    {
        Available = true;
        StartTime = -1.0f;
        EndTime = -1.0f;
    }

    /// <summary>
    /// 设置淡出开始。
    /// </summary>
    /// <param name="fadeOutSeconds">淡出所需时间[秒]</param>
    public void SetFadeout(float fadeOutSeconds)
    {
        FadeOutSeconds = fadeOutSeconds;
        IsTriggeredFadeOut = true;
    }

    /// <summary>
    /// 开始淡出。
    /// </summary>
    /// <param name="fadeOutSeconds">淡出所需时间[秒]</param>
    /// <param name="userTimeSeconds">居陷时间累计値[秒]</param>
    public void StartFadeout(float fadeOutSeconds, float userTimeSeconds)
    {
        float newEndTimeSeconds = userTimeSeconds + fadeOutSeconds;
        IsTriggeredFadeOut = true;

        if (EndTime < 0.0f || newEndTimeSeconds < EndTime)
        {
            EndTime = newEndTimeSeconds;
        }
    }

    /// <summary>
    /// 设置动作状态。
    /// </summary>
    /// <param name="timeSeconds">当前时刻[秒]</param>
    /// <param name="weight">动作的权重</param>
    public void SetState(float timeSeconds, float weight)
    {
        StateTime = timeSeconds;
        StateWeight = weight;
    }
}
