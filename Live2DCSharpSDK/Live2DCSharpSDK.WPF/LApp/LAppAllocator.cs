using Live2DCSharpSDK.Framework;
using System.Runtime.InteropServices;

namespace Live2DCSharpSDK.WPF.LApp;

/// <summary>
/// 内存分配与释放接口的实现。
/// 由框架调用。
/// </summary>
public class LAppAllocator : ICubismAllocator
{
    /// <summary>
    /// 分配内存区域。
    /// </summary>
    /// <param name="size">要分配的大小。</param>
    /// <returns>指定的内存区域</returns>
    public unsafe IntPtr Allocate(int size)
    {
        return (IntPtr)NativeMemory.AllocZeroed((nuint)size);
    }

    /// <summary>
    /// 释放内存区域。
    /// </summary>
    /// <param name="memory">要释放的内存。</param>
    public unsafe void Deallocate(IntPtr memory)
    {
        NativeMemory.Free((void*)memory);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="size">要分配的大小。</param>
    /// <param name="alignment">对齐字节数。</param>
    /// <returns>对齐后的内存地址</returns>
    public unsafe IntPtr AllocateAligned(int size, int alignment)
    {
        return (IntPtr)NativeMemory.AlignedAlloc((nuint)size, (nuint)alignment);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="alignedMemory">要释放的对齐内存。</param>
    public unsafe void DeallocateAligned(IntPtr alignedMemory)
    {
        NativeMemory.AlignedFree((void*)alignedMemory);
    }
}
