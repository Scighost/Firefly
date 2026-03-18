using Live2DCSharpSDK.Framework.Model;
using System.Text.Json.Nodes;

namespace Live2DCSharpSDK.Framework.Motion;

/// <summary>
/// 表情动作类。
/// </summary>
public class CubismExpressionMotion : ACubismMotion
{
    /// <summary>
    /// 加算应用的初始値
    /// </summary>
    public const float DefaultAdditiveValue = 0.0f;
    /// <summary>
    /// 乘算应用的初始値
    /// </summary>
    public const float DefaultMultiplyValue = 1.0f;

    public const string ExpressionKeyFadeIn = "FadeInTime";
    public const string ExpressionKeyFadeOut = "FadeOutTime";
    public const string ExpressionKeyParameters = "Parameters";
    public const string ExpressionKeyId = "Id";
    public const string ExpressionKeyValue = "Value";
    public const string ExpressionKeyBlend = "Blend";
    public const string BlendValueAdd = "Add";
    public const string BlendValueMultiply = "Multiply";
    public const string BlendValueOverwrite = "Overwrite";
    public const float DefaultFadeTime = 1.0f;

    /// <summary>
    /// 表情参数信息列表
    /// </summary>
    public List<ExpressionParameter> Parameters { get; init; } = [];

    /// <summary>
    /// 表情淡入淡出的权重値
    /// </summary>
    [Obsolete("CubismExpressionMotion._fadeWeight计划删除，不建议使用\nCubismExpressionMotionManager.getFadeWeight(int index) 代替。")]
    public float FadeWeight { get; private set; }

    /// <summary>
    /// 创建实例。
    /// </summary>
    /// <param name="buf">已加载 exp 文件的缓冲区</param>
    public CubismExpressionMotion(string buf)
    {
        using var stream = File.Open(buf, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var obj = JsonNode.Parse(stream) ?? throw new Exception("Load ExpressionMotion error");
        var json = obj.AsObject();

        FadeInSeconds = json.ContainsKey(ExpressionKeyFadeIn)
            ? (float)json[ExpressionKeyFadeIn]! : DefaultFadeTime;   // 淡入
        FadeOutSeconds = json.ContainsKey(ExpressionKeyFadeOut)
            ? (float)json[ExpressionKeyFadeOut]! : DefaultFadeTime; // 淡出

        if (FadeInSeconds < 0.0f)
        {
            FadeInSeconds = DefaultFadeTime;
        }

        if (FadeOutSeconds < 0.0f)
        {
            FadeOutSeconds = DefaultFadeTime;
        }

        // 逐一处理每个参数
        var list = json[ExpressionKeyParameters]!;
        int parameterCount = list.AsArray().Count;

        for (int i = 0; i < parameterCount; ++i)
        {
            var param = list[i]!;
            var parameterId = CubismFramework.CubismIdManager.GetId(param[ExpressionKeyId]!.ToString()); // 参数 ID
            var value = (float)param[ExpressionKeyValue]!; // 値

            // 设置计算方式
            ExpressionBlendType blendType;
            var type = param[ExpressionKeyBlend]?.ToString();
            if (type == null || type == BlendValueAdd)
            {
                blendType = ExpressionBlendType.Add;
            }
            else if (type == BlendValueMultiply)
            {
                blendType = ExpressionBlendType.Multiply;
            }
            else if (type == BlendValueOverwrite)
            {
                blendType = ExpressionBlendType.Overwrite;
            }
            else
            {
                // 其他：设置了规格外的値时，以加算模式恢复
                blendType = ExpressionBlendType.Add;
            }

            // 创建设置对象并添加到列表
            Parameters.Add(new()
            {
                ParameterId = parameterId,
                BlendType = blendType,
                Value = value
            });
        }
    }

    public override void DoUpdateParameters(CubismModel model, float userTimeSeconds, float weight, CubismMotionQueueEntry motionQueueEntry)
    {
        foreach (var item in Parameters)
        {
            switch (item.BlendType)
            {
                case ExpressionBlendType.Add:
                    {
                        model.AddParameterValue(item.ParameterId, item.Value, weight);            // 相对变化 加算
                        break;
                    }
                case ExpressionBlendType.Multiply:
                    {
                        model.MultiplyParameterValue(item.ParameterId, item.Value, weight);       // 相对变化 乘算
                        break;
                    }
                case ExpressionBlendType.Overwrite:
                    {
                        model.SetParameterValue(item.ParameterId, item.Value, weight);            // 绝对变化 覆盖写入
                        break;
                    }
                default:
                    // 设置了规格外的値时已是加算模式
                    break;
            }
        }
    }

    /// <summary>
    /// 计算与表情相关的模型参数。
    /// </summary>
    /// <param name="model">目标模型</param>
    /// <param name="userTimeSeconds">目标模型时间</param>
    /// <param name="motionQueueEntry">CubismMotionQueueManager 中管理的动作</param>
    /// <param name="expressionParameterValues">应用到模型的各参数値</param>
    /// <param name="expressionIndex">表情索引</param>
    public void CalculateExpressionParameters(CubismModel model, float userTimeSeconds, CubismMotionQueueEntry? motionQueueEntry,
    List<ExpressionParameterValue>? expressionParameterValues, int expressionIndex, float fadeWeight)
    {
        if (motionQueueEntry == null || expressionParameterValues == null)
        {
            return;
        }

        if (!motionQueueEntry.Available)
        {
            return;
        }

        // CubismExpressionMotion._fadeWeight 计划删除。
        // 为兼容性保留处理，但实际不使用。
        FadeWeight = UpdateFadeWeight(motionQueueEntry, userTimeSeconds);

        // 计算应用到模型的各参数値
        for (int i = 0; i < expressionParameterValues.Count; ++i)
        {
            ExpressionParameterValue expressionParameterValue = expressionParameterValues[i];

            if (expressionParameterValue.ParameterId == null)
            {
                continue;
            }

            float currentParameterValue = expressionParameterValue.OverwriteValue =
                model.GetParameterValue(expressionParameterValue.ParameterId);

            var expressionParameters = Parameters;
            int parameterIndex = -1;
            for (int j = 0; j < expressionParameters.Count; ++j)
            {
                if (expressionParameterValue.ParameterId != expressionParameters[j].ParameterId)
                {
                    continue;
                }

                parameterIndex = j;

                break;
            }

            // 正在播放的 Expression 未引用的参数应用初始値
            if (parameterIndex < 0)
            {
                if (expressionIndex == 0)
                {
                    expressionParameterValues[i].AdditiveValue = DefaultAdditiveValue;

                    expressionParameterValues[i].MultiplyValue = DefaultMultiplyValue;

                    expressionParameterValues[i].OverwriteValue = currentParameterValue;
                }
                else
                {
                    expressionParameterValues[i].AdditiveValue =
                        CalculateValue(expressionParameterValue.AdditiveValue, DefaultAdditiveValue, fadeWeight);

                    expressionParameterValues[i].MultiplyValue =
                        CalculateValue(expressionParameterValue.MultiplyValue, DefaultMultiplyValue, fadeWeight);

                    expressionParameterValues[i].OverwriteValue =
                        CalculateValue(expressionParameterValue.OverwriteValue, currentParameterValue, fadeWeight);
                }
                continue;
            }

            // 计算倇
            float value = expressionParameters[parameterIndex].Value;
            float newAdditiveValue, newMultiplyValue, newSetValue;
            switch (expressionParameters[parameterIndex].BlendType)
            {
                case ExpressionBlendType.Add:
                    newAdditiveValue = value;
                    newMultiplyValue = DefaultMultiplyValue;
                    newSetValue = currentParameterValue;
                    break;
                case ExpressionBlendType.Multiply:
                    newAdditiveValue = DefaultAdditiveValue;
                    newMultiplyValue = value;
                    newSetValue = currentParameterValue;
                    break;
                case ExpressionBlendType.Overwrite:
                    newAdditiveValue = DefaultAdditiveValue;
                    newMultiplyValue = DefaultMultiplyValue;
                    newSetValue = value;
                    break;
                default:
                    return;
            }

            if (expressionIndex == 0)
            {
                expressionParameterValues[i].AdditiveValue = newAdditiveValue;
                expressionParameterValues[i].MultiplyValue = newMultiplyValue;
                expressionParameterValues[i].OverwriteValue = newSetValue;
            }
            else
            {
                expressionParameterValues[i].AdditiveValue = (expressionParameterValue.AdditiveValue * (1.0f - FadeWeight)) + newAdditiveValue * FadeWeight;
                expressionParameterValues[i].MultiplyValue = (expressionParameterValue.MultiplyValue * (1.0f - FadeWeight)) + newMultiplyValue * FadeWeight;
                expressionParameterValues[i].OverwriteValue = (expressionParameterValue.OverwriteValue * (1.0f - FadeWeight)) + newSetValue * FadeWeight;
            }
        }
    }

    private float CalculateValue(float source, float destination, float fadeWeight)
    {
        return (source * (1.0f - fadeWeight)) + (destination * fadeWeight);
    }
}
