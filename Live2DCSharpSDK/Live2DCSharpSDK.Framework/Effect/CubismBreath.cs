using Live2DCSharpSDK.Framework.Model;

namespace Live2DCSharpSDK.Framework.Effect;

/// <summary>
/// 提供呼吸功能。
/// </summary>
public class CubismBreath
{
    /// <summary>
    /// 与呼吸关联的参数列表
    /// </summary>
    public required List<BreathParameterData> Parameters { get; init; }
    /// <summary>
    /// 累计时间[秒]
    /// </summary>
    private float _currentTime;

    /// <summary>
    /// 更新模型的参数。
    /// </summary>
    /// <param name="model">目标模型</param>
    /// <param name="deltaTimeSeconds">增量时间[秒]</param>
    public void UpdateParameters(CubismModel model, float deltaTimeSeconds)
    {
        _currentTime += deltaTimeSeconds;

        float t = _currentTime * 2.0f * 3.14159f;

        foreach (var item in Parameters)
        {
            model.AddParameterValue(item.ParameterId, item.Offset +
                (item.Peak * MathF.Sin(t / item.Cycle)), item.Weight);
        }
    }
}
