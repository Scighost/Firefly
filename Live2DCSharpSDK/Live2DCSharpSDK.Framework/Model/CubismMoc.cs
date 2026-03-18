using Live2DCSharpSDK.Framework.Core;
using System.Runtime.InteropServices;

namespace Live2DCSharpSDK.Framework.Model;

/// <summary>
/// 用于管理 Moc 数据的类。
/// </summary>
public class CubismMoc : IDisposable
{
    /// <summary>
    /// Moc 数据
    /// </summary>
    private readonly IntPtr _moc;
    /// <summary>
    /// 已读取模型的 .moc3 版本
    /// </summary>
    public uint MocVersion { get; }

    public CubismModel Model { get; }

    /// <summary>
    /// 从缓冲区读取 Moc 文件并创建 Moc 数据。
    /// </summary>
    /// <param name="mocBytes">Moc 文件的缓冲区</param>
    /// <param name="shouldCheckMocConsistency">MOC 的一致性检查标志（默认：false）</param>
    /// <returns></returns>
    public CubismMoc(byte[] mocBytes, bool shouldCheckMocConsistency = false)
    {
        IntPtr alignedBuffer = CubismFramework.AllocateAligned(mocBytes.Length, CsmEnum.csmAlignofMoc);
        Marshal.Copy(mocBytes, 0, alignedBuffer, mocBytes.Length);

        if (shouldCheckMocConsistency)
        {
            // 检查 .moc3 的一致性
            bool consistency = HasMocConsistency(alignedBuffer, mocBytes.Length);
            if (!consistency)
            {
                CubismFramework.DeallocateAligned(alignedBuffer);

                // 如果无法确认一致性则不继续处理
                throw new Exception("Inconsistent MOC3.");
            }
        }

        var moc = CubismCore.ReviveMocInPlace(alignedBuffer, mocBytes.Length);

        if (moc == IntPtr.Zero)
        {
            throw new Exception("MOC3 is null");
        }

        _moc = moc;

        MocVersion = CubismCore.GetMocVersion(alignedBuffer, mocBytes.Length);

        var modelSize = CubismCore.GetSizeofModel(_moc);
        var modelMemory = CubismFramework.AllocateAligned(modelSize, CsmEnum.CsmAlignofModel);

        var model = CubismCore.InitializeModelInPlace(_moc, modelMemory, modelSize);

        if (model == IntPtr.Zero)
        {
            throw new Exception("MODEL is null");
        }

        Model = new CubismModel(model);
    }

    /// <summary>
    /// 获取最新的 .moc3 版本。
    /// </summary>
    /// <returns></returns>
    public static uint GetLatestMocVersion()
    {
        return CubismCore.GetLatestMocVersion();
    }

    /// <summary>
    /// Checks consistency of a moc.
    /// </summary>
    /// <param name="address">Address of unrevived moc. The address must be aligned to 'csmAlignofMoc'.</param>
    /// <param name="size">Size of moc (in bytes).</param>
    /// <returns>'1' if Moc is valid; '0' otherwise.</returns>
    public static bool HasMocConsistency(IntPtr address, int size)
    {
        return CubismCore.HasMocConsistency(address, size);
    }

    /// <summary>
    /// 检查未复原的 moc 的一致性。
    /// </summary>
    /// <param name="data">Moc 文件的缓冲区</param>
    /// <returns>'true' if Moc is valid; 'false' otherwise.</returns>
    public static bool HasMocConsistencyFromUnrevivedMoc(byte[] data)
    {
        IntPtr alignedBuffer = CubismFramework.AllocateAligned(data.Length, CsmEnum.csmAlignofMoc);
        Marshal.Copy(data, 0, alignedBuffer, data.Length);

        bool consistency = HasMocConsistency(alignedBuffer, data.Length);

        CubismFramework.DeallocateAligned(alignedBuffer);

        return consistency;
    }

    /// <summary>
    /// 释放资源。
    /// </summary>
    public void Dispose()
    {
        Model.Dispose();
        CubismFramework.DeallocateAligned(_moc);
        GC.SuppressFinalize(this);
    }
}
