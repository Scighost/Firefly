// 指定 ID 的眼睛参数，在 0 时闭合则为 true，在 1 时闭合则为 false。
//#define CloseIfZero

using Live2DCSharpSDK.Framework.Model;

namespace Live2DCSharpSDK.Framework.Effect;

/// <summary>
/// 提供自动眨眼功能。
/// </summary>
public class CubismEyeBlink
{
    /// <summary>
    /// 操作对象的参数 ID 列表
    /// </summary>
    public readonly List<string> ParameterIds = [];
    /// <summary>
    /// 当前状态
    /// </summary>
    private EyeState _blinkingState;
    /// <summary>
    /// 下一次眨眼的时间[秒]
    /// </summary>
    private float _nextBlinkingTime;
    /// <summary>
    /// 当前状态开始的时间[秒]
    /// </summary>
    private float _stateStartTimeSeconds;
    /// <summary>
    /// 眨眼的间隔[秒]
    /// </summary>
    private float _blinkingIntervalSeconds;
    /// <summary>
    /// 闭眼动作所需时间[秒]
    /// </summary>
    private float _closingSeconds;
    /// <summary>
    /// 眼睑闭合保持时间[秒]
    /// </summary>
    private float _closedSeconds;
    /// <summary>
    /// 睁开眼动作所需时间[秒]
    /// </summary>
    private float _openingSeconds;
    /// <summary>
    /// 累计的增量时间[秒]
    /// </summary>
    private float _userTimeSeconds;

    /// <summary>
    /// 创建实例。
    /// </summary>
    /// <param name="modelSetting">模型的设置信息</param>
    public CubismEyeBlink(ModelSettingObj modelSetting)
    {
        _blinkingState = EyeState.First;
        _blinkingIntervalSeconds = 4.0f;
        _closingSeconds = 0.1f;
        _closedSeconds = 0.05f;
        _openingSeconds = 0.15f;

        foreach (var item in modelSetting.Groups)
        {
            if (item.Name == CubismModelSettingJson.EyeBlink)
            {
                foreach (var item1 in item.Ids)
                {
                    if (item1 == null)
                        continue;
                    var item2 = CubismFramework.CubismIdManager.GetId(item1);
                    ParameterIds.Add(item2);
                }
                break;
            }
        }
    }

    /// <summary>
    /// 设置眨眼间隔。
    /// </summary>
    /// <param name="blinkingInterval">真眼间隔时间（秒）</param>
    public void SetBlinkingInterval(float blinkingInterval)
    {
        _blinkingIntervalSeconds = blinkingInterval;
    }

    /// <summary>
    /// 设置眨眼动作的详细参数。
    /// </summary>
    /// <param name="closing">闭眼动作所需时间（秒）</param>
    /// <param name="closed">保持闭眼状态的时间（秒）</param>
    /// <param name="opening">开眼动作所需时间（秒）</param>
    public void SetBlinkingSettings(float closing, float closed, float opening)
    {
        _closingSeconds = closing;
        _closedSeconds = closed;
        _openingSeconds = opening;
    }

    /// <summary>
    /// 更新模型的参数。
    /// </summary>
    /// <param name="model">目标模型</param>
    /// <param name="deltaTimeSeconds">增量时间（秒）</param>
    public void UpdateParameters(CubismModel model, float deltaTimeSeconds)
    {
        _userTimeSeconds += deltaTimeSeconds;
        float parameterValue;
        float t;
        switch (_blinkingState)
        {
            case EyeState.Closing:
                t = ((_userTimeSeconds - _stateStartTimeSeconds) / _closingSeconds);

                if (t >= 1.0f)
                {
                    t = 1.0f;
                    _blinkingState = EyeState.Closed;
                    _stateStartTimeSeconds = _userTimeSeconds;
                }

                parameterValue = 1.0f - t;

                break;
            case EyeState.Closed:
                t = ((_userTimeSeconds - _stateStartTimeSeconds) / _closedSeconds);

                if (t >= 1.0f)
                {
                    _blinkingState = EyeState.Opening;
                    _stateStartTimeSeconds = _userTimeSeconds;
                }

                parameterValue = 0.0f;

                break;
            case EyeState.Opening:
                t = ((_userTimeSeconds - _stateStartTimeSeconds) / _openingSeconds);

                if (t >= 1.0f)
                {
                    t = 1.0f;
                    _blinkingState = EyeState.Interval;
                    _nextBlinkingTime = DetermineNextBlinkingTiming();
                }

                parameterValue = t;

                break;
            case EyeState.Interval:
                if (_nextBlinkingTime < _userTimeSeconds)
                {
                    _blinkingState = EyeState.Closing;
                    _stateStartTimeSeconds = _userTimeSeconds;
                }

                parameterValue = 1.0f;

                break;
            case EyeState.First:
            default:
                _blinkingState = EyeState.Interval;
                _nextBlinkingTime = DetermineNextBlinkingTiming();

                parameterValue = 1.0f;

                break;
        }
#if CloseIfZero
        parameterValue = -parameterValue;
#endif

        foreach (var item in ParameterIds)
        {
            model.SetParameterValue(item, parameterValue);
        }
    }

    /// <summary>
    /// 决定下一次眨眼的时机。
    /// </summary>
    /// <returns>下次真眼的时刻（秒）</returns>
    private float DetermineNextBlinkingTiming()
    {
        float r = Random.Shared.NextSingle();

        return _userTimeSeconds + (r * (2.0f * _blinkingIntervalSeconds - 1.0f));
    }
}
