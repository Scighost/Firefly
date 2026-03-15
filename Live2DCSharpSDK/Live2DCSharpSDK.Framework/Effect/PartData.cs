using Live2DCSharpSDK.Framework.Model;

namespace Live2DCSharpSDK.Framework.Effect;


/// <summary>
/// 管理部件相关的各种数据。
/// </summary>
public record PartData
{
    /// <summary>
    /// 部件 ID
    /// </summary>
    public required string PartId { get; set; }
    /// <summary>
    /// 参数的索引
    /// </summary>
    public int ParameterIndex { get; set; }
    /// <summary>
    /// 部件的索引
    /// </summary>
    public int PartIndex { get; set; }
    /// <summary>
    /// 关联的参数
    /// </summary>
    public readonly List<PartData> Link = [];

    /// <summary>
    /// 进行初始化。
    /// </summary>
    /// <param name="model">用于初始化的模型</param>
    public void Initialize(CubismModel model)
    {
        ParameterIndex = model.GetParameterIndex(PartId);
        PartIndex = model.GetPartIndex(PartId);

        model.SetParameterValue(ParameterIndex, 1);
    }
}
