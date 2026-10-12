using System;
using System.Runtime.InteropServices;

namespace Robust.Client.Graphics.Fonts;

internal unsafe interface IFontMemoryHandle : IDisposable
{
    byte* GetData();
    nint GetDataSize();
}

internal sealed class ArrayFontMemoryHandle(byte[] array) : IFontMemoryHandle
{
    private GCHandle _gcHandle = GCHandle.Alloc(array, GCHandleType.Pinned);

    public unsafe byte* GetData()
    {
        return (byte*)_gcHandle.AddrOfPinnedObject();
    }

    public IntPtr GetDataSize()
    {
        return array.Length;
    }

    public void Dispose()
    {
        _gcHandle.Free();
        _gcHandle = default;
        GC.SuppressFinalize(this);
    }

    ~ArrayFontMemoryHandle()
    {
        Dispose();
    }
}
