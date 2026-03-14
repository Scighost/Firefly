using Live2DCSharpSDK.Framework;
using System;
using System.Runtime.InteropServices;

namespace Live2DCSharpSDK.WinUI.LApp;

/// <summary>
/// メモリ確保・解放処理のインターフェースの実装。
/// フレームワークから呼び出される。
/// </summary>
public class LAppAllocator : ICubismAllocator
{
    /// <summary>
    /// メモリ領域を割り当てる。
    /// </summary>
    /// <param name="size">割り当てたいサイズ。</param>
    /// <returns>指定したメモリ領域</returns>
    public unsafe IntPtr Allocate(int size)
    {
        return (IntPtr)NativeMemory.AllocZeroed((nuint)size);
    }

    /// <summary>
    /// メモリ領域を解放する
    /// </summary>
    /// <param name="memory">解放するメモリ。</param>
    public unsafe void Deallocate(IntPtr memory)
    {
        NativeMemory.Free((void*)memory);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="size">割り当てたいサイズ。</param>
    /// <param name="alignment">割り当てたいサイズ。</param>
    /// <returns>alignedAddress</returns>
    public unsafe IntPtr AllocateAligned(int size, int alignment)
    {
        return (IntPtr)NativeMemory.AlignedAlloc((nuint)size, (nuint)alignment);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="alignedMemory">解放するメモリ。</param>
    public unsafe void DeallocateAligned(IntPtr alignedMemory)
    {
        NativeMemory.AlignedFree((void*)alignedMemory);
    }
}
