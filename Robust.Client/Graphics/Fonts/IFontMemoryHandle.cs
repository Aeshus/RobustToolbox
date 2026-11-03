using System;
using System.IO;
using System.IO.MemoryMappedFiles;
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

internal sealed class MemoryMappedFontMemoryHandle : IFontMemoryHandle
{
    private readonly MemoryMappedFile _mappedFile;
    private readonly MemoryMappedViewAccessor _accessor;

    public MemoryMappedFontMemoryHandle(string filePath)
    {
        _mappedFile = MemoryMappedFile.CreateFromFile(
            filePath,
            FileMode.Open,
            null,
            0,
            MemoryMappedFileAccess.Read);

        _accessor = _mappedFile.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
    }

    public unsafe byte* GetData()
    {
        byte* pointer = null;
        _accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref pointer);
        return pointer;
    }

    public nint GetDataSize()
    {
        return (nint)_accessor.Capacity;
    }

    public void Dispose()
    {
        _accessor.Dispose();
        _mappedFile.Dispose();
    }
}

