namespace Live2DCSharpSDK.Framework.Model;

/// <summary>
/// 用于记录从 JSON 中读取的用户数据的结构体
/// </summary>
public record CubismModelUserDataNode
{
    /// <summary>
    /// 用户数据目标类型
    /// </summary>
    public required string TargetType { get; set; }
    /// <summary>
    /// 用户数据目标的 ID
    /// </summary>
    public required string TargetId { get; set; }
    /// <summary>
    /// 用户数据
    /// </summary>
    public required string Value { get; set; }
}
