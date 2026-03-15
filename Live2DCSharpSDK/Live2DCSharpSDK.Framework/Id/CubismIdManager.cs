namespace Live2DCSharpSDK.Framework.Id;

/// <summary>
/// 管理 ID 名称。
/// </summary>
public class CubismIdManager
{
    /// <summary>
    /// 已注册的 ID 列表
    /// </summary>
    private readonly List<string> _ids = [];

    /// <summary>
    /// 将 ID 名从列表中注册。
    /// </summary>
    /// <param name="list">ID 名列表</param>
    public void RegisterIds(List<string> list)
    {
        list.ForEach((item) =>
        {
            GetId(item);
        });
    }

    /// <summary>
    /// 从 ID 名获取 ID。
    /// 如果 ID 名尚未注册，则同时注册它。
    /// </summary>
    /// <param name="item">ID 名</param>
    public string GetId(string item)
    {
        if (_ids.Contains(item))
            return item;

        _ids.Add(item);

        return item;
    }
}
