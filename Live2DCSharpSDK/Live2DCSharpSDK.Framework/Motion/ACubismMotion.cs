using Live2DCSharpSDK.Framework.Math;
using Live2DCSharpSDK.Framework.Model;

namespace Live2DCSharpSDK.Framework.Motion;

/// <summary>
/// 动作的抽象基类，由 MotionQueueManager 管理动作的播放。
/// </summary>
public abstract class ACubismMotion
{
    /// <summary>
    /// 淡入所需时间｛秒｝
    /// </summary>
    public float FadeInSeconds { get; set; }
    /// <summary>
    /// 淡出所需时间｛秒｝
    /// </summary>
    public float FadeOutSeconds { get; set; }
    /// <summary>
    /// 动作的权重
    /// </summary>
    public float Weight { get; set; }
    /// <summary>
    /// 动作播放开始时刻｛秒｝
    /// </summary>
    public float OffsetSeconds { get; set; }

    protected readonly List<string> FiredEventValues = [];

    // 动作播放结束回调函数
    public FinishedMotionCallback? OnFinishedMotion { get; set; }

    /// <summary>
    /// 构造函数。
    /// </summary>
    public ACubismMotion()
    {
        FadeInSeconds = -1.0f;
        FadeOutSeconds = -1.0f;
        Weight = 1.0f;
    }

    /// <summary>
    /// 更新模型参数。
    /// </summary>
    /// <param name="model">目标模型</param>
    /// <param name="motionQueueEntry">CubismMotionQueueManager 中管理的动作</param>
    /// <param name="userTimeSeconds">居陷时间的累计値｛秒｝</param>
    public void UpdateParameters(CubismModel model, CubismMotionQueueEntry motionQueueEntry, float userTimeSeconds)
    {
        if (!motionQueueEntry.Available || motionQueueEntry.Finished)
        {
            return;
        }

        SetupMotionQueueEntry(motionQueueEntry, userTimeSeconds);

        var fadeWeight = UpdateFadeWeight(motionQueueEntry, userTimeSeconds);

        //---- 遍历所有参数 ID ----
        DoUpdateParameters(model, userTimeSeconds, fadeWeight, motionQueueEntry);

        //后处理
        //超过结束时刻时置位结束标志（CubismMotionQueueManager）
        if ((motionQueueEntry.EndTime > 0) && (motionQueueEntry.EndTime < userTimeSeconds))
        {
            motionQueueEntry.Finished = true;      //结束
        }
    }

    /// <summary>
    /// 进行动作播放的初期化设置。
    /// </summary>
    /// <param name="motionQueueEntry">CubismMotionQueueManager 管理的动作</param>
    /// <param name="userTimeSeconds">总播放时间（秒）</param>
    public void SetupMotionQueueEntry(CubismMotionQueueEntry motionQueueEntry, float userTimeSeconds)
    {
        if (!motionQueueEntry.Available || motionQueueEntry.Finished)
        {
            return;
        }

        if (motionQueueEntry.Started)
        {
            return;
        }

        motionQueueEntry.Started = true;
        motionQueueEntry.StartTime = userTimeSeconds - OffsetSeconds; //记录动作的开始时刻
        motionQueueEntry.FadeInStartTime = userTimeSeconds; //淡入开始时刻

        var duration = GetDuration();

        if (motionQueueEntry.EndTime < 0)
        {
            //开始前已设置结束的情况。
            motionQueueEntry.EndTime = (duration <= 0) ? -1 : motionQueueEntry.StartTime + duration;
            //duration == -1 时循环播放
        }
    }

    /// <summary>
    /// 更新动作权重。
    /// </summary>
    /// <param name="motionQueueEntry">CubismMotionQueueManager 中管理的动作</param>
    /// <param name="userTimeSeconds">居陷时间的累计値｛秒｝</param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public float UpdateFadeWeight(CubismMotionQueueEntry? motionQueueEntry, float userTimeSeconds)
    {
        if (motionQueueEntry == null)
        {
            CubismLog.Error("[Live2D SDK]motionQueueEntry is null.");
            return 0;
        }

        float fadeWeight = Weight; //与当前值相乘的比例

        //---- 淡入/淡出处理 ----
        //使用简单的正弦函数进行缓动
        float fadeIn = FadeInSeconds == 0.0f ? 1.0f
                           : CubismMath.GetEasingSine((userTimeSeconds - motionQueueEntry.FadeInStartTime) / FadeInSeconds);

        float fadeOut = (FadeOutSeconds == 0.0f || motionQueueEntry.EndTime < 0.0f) ? 1.0f
                            : CubismMath.GetEasingSine((motionQueueEntry.EndTime - userTimeSeconds) / FadeOutSeconds);

        fadeWeight = fadeWeight * fadeIn * fadeOut;

        motionQueueEntry.SetState(userTimeSeconds, fadeWeight);

        if (0.0f > fadeWeight || fadeWeight > 1.0f)
        {
            throw new Exception("fadeWeight out of range");
        }

        return fadeWeight;
    }

    /// <summary>
    /// 获取动作长度。
    /// 
    /// 循环时返回 "-1"。
    /// 非循环时请重写此方法。
    /// 返回正値时在该时间后结束。
    /// 返回 "-1" 则除非外部发出停止指令，否则永不结束。
    /// </summary>
    /// <returns>动作长度｛秒｝</returns>
    public virtual float GetDuration()
    {
        return -1.0f;
    }

    /// <summary>
    /// 获取动作循环一次的长度。
    /// 
    /// 不循环时返回与 GetDuration() 相同的値。
    /// 无法定义循环单次长度时（如程序化连续运动的子类）返回 "-1"。
    /// </summary>
    /// <returns>动作循环一次的长度｛秒｝</returns>
    public virtual float GetLoopDuration()
    {
        return -1.0f;
    }

    /// <summary>
    /// 检测事件是否触发。
    /// 输入时间以该动作调用时刻为 0 的秒数进行计算。
    /// </summary>
    /// <param name="beforeCheckTimeSeconds">上一次事件检测时间｛秒｝</param>
    /// <param name="motionTimeSeconds">本次播放时间｛秒｝</param>
    /// <returns></returns>
    public virtual List<string> GetFiredEvent(float beforeCheckTimeSeconds, float motionTimeSeconds)
    {
        return FiredEventValues;
    }

    /// <summary>
    /// 检查是否存在不透明度曲线
    /// </summary>
    /// <returns>true  . 存在键
    /// false . 不存在键</returns>
    public virtual bool IsExistModelOpacity()
    {
        return false;
    }

    /// <summary>
    /// 返回不透明度曲线的索引
    /// </summary>
    /// <returns>success：不透明度曲线的索引</returns>
    public virtual int GetModelOpacityIndex()
    {
        return -1;
    }

    /// <summary>
    /// 返回不透明度的 Id
    /// </summary>
    /// <returns>不透明度的 Id</returns>
    public virtual string? GetModelOpacityId(int index)
    {
        return "";
    }

    public virtual float GetModelOpacityValue()
    {
        return 1.0f;
    }

    public abstract void DoUpdateParameters(CubismModel model, float userTimeSeconds, float weight, CubismMotionQueueEntry motionQueueEntry);
}
