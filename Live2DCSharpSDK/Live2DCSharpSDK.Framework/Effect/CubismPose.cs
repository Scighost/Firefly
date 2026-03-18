using Live2DCSharpSDK.Framework.Model;
using System.Text.Json.Nodes;

namespace Live2DCSharpSDK.Framework.Effect;

/// <summary>
/// 管理并设置部件的不透明度。
/// </summary>
public class CubismPose
{
    public const float Epsilon = 0.001f;
    public const float DefaultFadeInSeconds = 0.5f;

    // Pose.json 的标签
    public const string FadeIn = "FadeInTime";
    public const string Link = "Link";
    public const string Groups = "Groups";
    public const string Id = "Id";

    /// <summary>
    /// 部件组
    /// </summary>
    private readonly List<PartData> _partGroups = [];
    /// <summary>
    /// 各部件组的数量
    /// </summary>
    private readonly List<int> _partGroupCounts = [];
    /// <summary>
    /// 淡入时间[秒]
    /// </summary>
    private readonly float _fadeTimeSeconds = DefaultFadeInSeconds;
    /// <summary>
    /// 上次操作的模型
    /// </summary>
    private CubismModel? _lastModel;

    /// <summary>
    /// 创建实例。
    /// </summary>
    /// <param name="pose3json">pose3.json 的数据</param>
    public CubismPose(string pose3json)
    {
        using var stream = File.Open(pose3json, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var json = JsonNode.Parse(stream)?.AsObject()
            ?? throw new Exception("Pose json is error");

        // 指定淡入时间
        if (json.ContainsKey(FadeIn))
        {
            var item = json[FadeIn];
            _fadeTimeSeconds = item == null ? DefaultFadeInSeconds : (float)item;

            if (_fadeTimeSeconds < 0.0f)
            {
                _fadeTimeSeconds = DefaultFadeInSeconds;
            }
        }

        // 部件组
        if (json[Groups] is not JsonArray poseListInfo)
            return;

        foreach (var item in poseListInfo)
        {
            int idCount = item!.AsArray().Count;
            int groupCount = 0;

            for (int groupIndex = 0; groupIndex < idCount; ++groupIndex)
            {
                var partInfo = item[groupIndex]!;
                PartData partData = new()
                {
                    PartId = CubismFramework.CubismIdManager.GetId(partInfo[Id]!.ToString())
                };

                // 设置关联部件
                if (partInfo[Link] != null)
                {
                    var linkListInfo = partInfo[Link]!;
                    int linkCount = linkListInfo.AsArray().Count;

                    for (int linkIndex = 0; linkIndex < linkCount; ++linkIndex)
                    {
                        partData.Link.Add(new()
                        {
                            PartId = CubismFramework.CubismIdManager.GetId(linkListInfo[linkIndex]!.ToString())
                        });
                    }
                }

                _partGroups.Add(partData);

                ++groupCount;
            }

            _partGroupCounts.Add(groupCount);
        }
    }

    /// <summary>
    /// 更新模型的参数。
    /// </summary>
    /// <param name="model">目标模型</param>
    /// <param name="deltaTimeSeconds">增量时间[秒]</param>
    public void UpdateParameters(CubismModel model, float deltaTimeSeconds)
    {
        // 当与上次模型不同的时候需要初始化
        if (model.Model != _lastModel?.Model)
        {
            // 初始化参数索引
            Reset(model);
        }

        _lastModel = model;

        // 由于从设置中更改时间可能导致经过时间为负数，因此将其视为 0 处理。
        if (deltaTimeSeconds < 0.0f)
        {
            deltaTimeSeconds = 0.0f;
        }

        int beginIndex = 0;

        foreach (var item in _partGroupCounts)
        {
            DoFade(model, deltaTimeSeconds, beginIndex, item);

            beginIndex += item;
        }

        CopyPartOpacities(model);
    }

    /// <summary>
    /// 初始化显示。
    /// 
    /// 对初始不透明度不为0的参数，将不透明度设为1。
    /// </summary>
    /// <param name="model">目标模型</param>
    public void Reset(CubismModel model)
    {
        int beginIndex = 0;

        foreach (var item in _partGroupCounts)
        {
            for (int j = beginIndex; j < beginIndex + item; ++j)
            {
                _partGroups[j].Initialize(model);

                int partsIndex = _partGroups[j].PartIndex;
                int paramIndex = _partGroups[j].ParameterIndex;

                if (partsIndex < 0)
                {
                    continue;
                }

                model.SetPartOpacity(partsIndex, j == beginIndex ? 1.0f : 0.0f);
                model.SetParameterValue(paramIndex, j == beginIndex ? 1.0f : 0.0f);

                for (int k = 0; k < _partGroups[j].Link.Count; ++k)
                {
                    _partGroups[j].Link[k].Initialize(model);
                }
            }

            beginIndex += item;
        }
    }

    /// <summary>
    /// 复制部件的不透明度并设置到关联的部件上。
    /// </summary>
    /// <param name="model">目标模型</param>
    private void CopyPartOpacities(CubismModel model)
    {
        foreach (var item in _partGroups)
        {
            if (item.Link.Count == 0)
            {
                continue; // 没有关联的参数
            }

            int partIndex = item.PartIndex;
            float opacity = model.GetPartOpacity(partIndex);

            foreach (var item1 in item.Link)
            {
                int linkPartIndex = item1.PartIndex;

                if (linkPartIndex < 0)
                {
                    continue;
                }

                model.SetPartOpacity(linkPartIndex, opacity);
            }
        }
    }

    /// <summary>
    /// 对部件执行淡入淡出操作。
    /// </summary>
    /// <param name="model">目标模型</param>
    /// <param name="deltaTimeSeconds">增量时间[秒]</param>
    /// <param name="beginIndex">要执行淡入淡出操作的部件组起始索引</param>
    /// <param name="partGroupCount">要执行淡入淡出操作的部件组数量</param>
    private void DoFade(CubismModel model, float deltaTimeSeconds, int beginIndex, int partGroupCount)
    {
        int visiblePartIndex = -1;
        float newOpacity = 1.0f;

        float Phi = 0.5f;
        float BackOpacityThreshold = 0.15f;

        // 获取当前处于显示状态的部件
        for (int i = beginIndex; i < beginIndex + partGroupCount; ++i)
        {
            int partIndex = _partGroups[i].PartIndex;
            int paramIndex = _partGroups[i].ParameterIndex;

            if (model.GetParameterValue(paramIndex) > Epsilon)
            {
                if (visiblePartIndex >= 0)
                {
                    break;
                }

                visiblePartIndex = i;
                newOpacity = model.GetPartOpacity(partIndex);

                // 计算新的不透明度
                newOpacity += deltaTimeSeconds / _fadeTimeSeconds;

                if (newOpacity > 1.0f)
                {
                    newOpacity = 1.0f;
                }
            }
        }

        if (visiblePartIndex < 0)
        {
            visiblePartIndex = 0;
            newOpacity = 1.0f;
        }

        // 设置显示部件与非显示部件的不透明度
        for (int i = beginIndex; i < beginIndex + partGroupCount; ++i)
        {
            int partsIndex = _partGroups[i].PartIndex;

            // 显示部件的设置
            if (visiblePartIndex == i)
            {
                model.SetPartOpacity(partsIndex, newOpacity); // 先设置
            }
            // 非显示部件的设置
            else
            {
                float opacity = model.GetPartOpacity(partsIndex);
                float a1;          // 通过计算得到的不透明度

                if (newOpacity < Phi)
                {
                    a1 = newOpacity * (Phi - 1) / Phi + 1.0f; // 通过点 (0,1) 和 (phi,phi) 的直线公式
                }
                else
                {
                    a1 = (1 - newOpacity) * Phi / (1.0f - Phi); // 通过点 (1,0) 和 (phi,phi) 的直线公式
                }

                // 若要限制背景可见比例
                float backOpacity = (1.0f - a1) * (1.0f - newOpacity);

                if (backOpacity > BackOpacityThreshold)
                {
                    a1 = 1.0f - BackOpacityThreshold / (1.0f - newOpacity);
                }

                if (opacity > a1)
                {
                    opacity = a1; // 如果不透明度大于计算得到的值，则将其设为计算值
                }

                model.SetPartOpacity(partsIndex, opacity);
            }
        }
    }
}
