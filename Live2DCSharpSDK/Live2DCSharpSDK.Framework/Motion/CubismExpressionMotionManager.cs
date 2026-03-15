using Live2DCSharpSDK.Framework.Math;
using Live2DCSharpSDK.Framework.Model;

namespace Live2DCSharpSDK.Framework.Motion;

/// <summary>
/// 应用到参数的表情偷的结构体
/// </summary>
public record ExpressionParameterValue
{
    /// <summary>
    /// 参数 ID
    /// </summary>
    public string ParameterId;
    /// <summary>
    /// 加算側
    /// </summary>
    public float AdditiveValue;
    /// <summary>
    /// 乘算値
    /// </summary>
    public float MultiplyValue;
    /// <summary>
    /// 覆盖写入值
    /// </summary>
    public float OverwriteValue;
};

public class CubismExpressionMotionManager : CubismMotionQueueManager
{
    // 应用到模型的各参数倗
    private readonly List<ExpressionParameterValue> _expressionParameterValues = [];
    // 正在播放的表情的权重假
    private readonly List<float> _fadeWeights = [];

    /// <summary>
    /// 当前正在播放的动作的优先级
    /// </summary>
    public MotionPriority CurrentPriority { get; private set; }
    /// <summary>
    /// 即将播放的动作的优先级，播放中时为 0。在其他线程读取动作文件时使用的功能。
    /// </summary>
    public MotionPriority ReservePriority { get; set; }

    /// <summary>
    /// 按优先级启动表情动作。
    /// </summary>
    /// <param name="motion">动作</param>
    /// <param name="priority">优先级</param>
    /// <returns>返回已启动动作的标识编号，用于 IsFinished() 的判断参数。无法启动时返回 "-1"。</returns>
    public CubismMotionQueueEntry StartMotionPriority(ACubismMotion motion, MotionPriority priority)
    {
        if (priority == ReservePriority)
        {
            ReservePriority = 0;           // 取消预约
        }
        CurrentPriority = priority;        // 设置正在播放的动作的优先级

        _fadeWeights.Add(0.0f);

        return StartMotion(motion, UserTimeSeconds);
    }

    /// <summary>
    /// 更新表情动作并将参数往应用到模型。
    /// </summary>
    /// <param name="model">目标模型</param>
    /// <param name="deltaTimeSeconds">居陷时间[秒]</param>
    /// <returns>true    已更新
    /// false   未更新</returns>
    public bool UpdateMotion(CubismModel model, float deltaTimeSeconds)
    {
        UserTimeSeconds += deltaTimeSeconds;
        bool updated = false;
        List<CubismMotionQueueEntry> motions = Motions;

        float expressionWeight = 0.0f;
        int expressionIndex = 0;

        // ------- 执行处理 --------
        // 如果已有动作，置位结束标志
        var list = new List<CubismMotionQueueEntry>();
        for (int i1 = 0; i1 < motions.Count; i1++)
        {
            CubismMotionQueueEntry? item = motions[i1];
            if (item.Motion is not CubismExpressionMotion expressionMotion)
            {
                list.Add(item);
                continue;
            }

            List<ExpressionParameter> expressionParameters = expressionMotion.Parameters;
            if (item.Available)
            {
                // 将正在播放的 Expression 引用的所有参数列入列表
                for (int i = 0; i < expressionParameters.Count; ++i)
                {
                    if (expressionParameters[i].ParameterId == null)
                    {
                        continue;
                    }

                    int index = -1;
                    // 在列表中搜索参数 ID 是否存在
                    for (int j = 0; j < _expressionParameterValues.Count; ++j)
                    {
                        if (_expressionParameterValues[j].ParameterId != expressionParameters[i].ParameterId)
                        {
                            continue;
                        }

                        index = j;
                        break;
                    }

                    if (index >= 0)
                    {
                        continue;
                    }

                    // 参数不在列表中则新建添加
                    ExpressionParameterValue item1 = new()
                    {
                        ParameterId = expressionParameters[i].ParameterId,
                        AdditiveValue = CubismExpressionMotion.DefaultAdditiveValue,
                        MultiplyValue = CubismExpressionMotion.DefaultMultiplyValue
                    };
                    item1.OverwriteValue = model.GetParameterValue(item1.ParameterId);
                    _expressionParameterValues.Add(item1);
                }
            }

            // ------ 计算倇 ------
            expressionMotion.SetupMotionQueueEntry(item, UserTimeSeconds);
            _fadeWeights[expressionIndex] = expressionMotion.UpdateFadeWeight(item, UserTimeSeconds);
            expressionMotion.CalculateExpressionParameters(model, UserTimeSeconds,
                item, _expressionParameterValues, expressionIndex, _fadeWeights[expressionIndex]);

            expressionWeight += expressionMotion.FadeInSeconds == 0.0f
                ? 1.0f
                : CubismMath.GetEasingSine((UserTimeSeconds - item.FadeInStartTime) / expressionMotion.FadeInSeconds);

            updated = true;

            if (item.IsTriggeredFadeOut)
            {
                // 开始淡出
                item.StartFadeout(item.FadeOutSeconds, UserTimeSeconds);
            }

            ++expressionIndex;
        }

        // ----- 最新 Expression 的淡入完成后删除之前的 Expression ------
        if (motions.Count > 1)
        {
            float latestFadeWeight = _fadeWeights[_fadeWeights.Count - 1];
            if (latestFadeWeight >= 1.0f)
            {
                // 不删除数组最后一个元素
                for (int i = motions.Count - 2; i >= 0; i--)
                {
                    motions.RemoveAt(i);
                    _fadeWeights.RemoveAt(i);
                }
            }
        }

        if (expressionWeight > 1.0f)
        {
            expressionWeight = 1.0f;
        }

        // 将各倇应用到模型
        for (int i = 0; i < _expressionParameterValues.Count; ++i)
        {
            model.SetParameterValue(_expressionParameterValues[i].ParameterId,
                (_expressionParameterValues[i].OverwriteValue + _expressionParameterValues[i].AdditiveValue) * _expressionParameterValues[i].MultiplyValue,
                expressionWeight);

            _expressionParameterValues[i].AdditiveValue = CubismExpressionMotion.DefaultAdditiveValue;
            _expressionParameterValues[i].MultiplyValue = CubismExpressionMotion.DefaultMultiplyValue;
        }

        return updated;
    }

    /// <summary>
    /// 获取当前表情的淡入淡出权重値。
    /// </summary>
    /// <param name="index">要获取的表情动作索引</param>
    /// <returns>表情的淡入淡出权重値</returns>
    public float GetFadeWeight(int index)
    {
        return _fadeWeights[index];
    }
}
