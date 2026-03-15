namespace Live2DCSharpSDK.Framework;

/// <summary>
/// 抽象化内存分配的接口。
///
/// 在平台端实现内存分配与释放处理，供框架调用的接口。
/// </summary>
public interface ICubismAllocator
{
    /// <summary>
    /// 分配无对齐约束的堆内存。
    /// </summary>
    /// <param name="size">要分配的字节数</param>
    /// <returns>成功时返回分配到的内存地址；否则返回 '0'。</returns>
    IntPtr Allocate(int size);

    /// <summary>
    /// 释放无对齐约束的堆内存。
    /// </summary>
    /// <param name="memory">要释放的内存地址</param>
    void Deallocate(IntPtr memory);

    /// <summary>
    /// 分配有对齐约束的堆内存。
    /// </summary>
    /// <param name="size">要分配的字节数</param>
    /// <param name="alignment">内存块的对齐宽度</param>
    /// <returns>成功时返回分配到的内存地址；否则返回 '0'。</returns>
    IntPtr AllocateAligned(int size, int alignment);

    /// <summary>
    /// 释放有对齐约束的堆内存。
    /// </summary>
    /// <param name="alignedMemory">要释放的对齐内存地址</param>
    void DeallocateAligned(IntPtr alignedMemory);
}
