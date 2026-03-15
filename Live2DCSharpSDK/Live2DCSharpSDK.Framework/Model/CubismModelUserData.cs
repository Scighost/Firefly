using System.Text.Json;

namespace Live2DCSharpSDK.Framework.Model;

/// <summary>
/// 负责用户数据的加载、管理、检索接口及释放。
/// </summary>
public class CubismModelUserData
{
    public const string ArtMesh = "ArtMesh";
    public const string Meta = "Meta";
    public const string UserDataCount = "UserDataCount";
    public const string TotalUserDataSize = "TotalUserDataSize";
    public const string UserData = "UserData";
    public const string Target = "Target";
    public const string Id = "Id";
    public const string Value = "Value";

    /// <summary>
    /// 用户数据结构体数组
    /// </summary>
    private readonly List<CubismModelUserDataNode> _userDataNodes = [];
    /// <summary>
    /// 访问列表保持
    /// </summary>
    public readonly List<CubismModelUserDataNode> ArtMeshUserDataNodes = [];

    /// <summary>
    /// 解析 userdata3.json。
    /// </summary>
    /// <param name="data">已加载 userdata3.json 的文件路径</param>
    public CubismModelUserData(string data)
    {
        using var stream = File.Open(data, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var obj = JsonSerializer.Deserialize(stream, CubismModelUserDataObjContext.Default.CubismModelUserDataObj)
            ?? throw new Exception("Load UserData error");

        string typeOfArtMesh = CubismFramework.CubismIdManager.GetId(ArtMesh);

        int nodeCount = obj.Meta.UserDataCount;

        for (int i = 0; i < nodeCount; i++)
        {
            var node = obj.UserData[i];
            CubismModelUserDataNode addNode = new()
            {
                TargetId = CubismFramework.CubismIdManager.GetId(node.Id),
                TargetType = CubismFramework.CubismIdManager.GetId(node.Target),
                Value = CubismFramework.CubismIdManager.GetId(node.Value)
            };
            _userDataNodes.Add(addNode);

            if (addNode.TargetType == typeOfArtMesh)
            {
                ArtMeshUserDataNodes.Add(addNode);
            }
        }
    }
}
