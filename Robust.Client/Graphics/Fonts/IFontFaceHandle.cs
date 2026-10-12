using SharpFont;

namespace Robust.Client.Graphics.Fonts;

internal interface IFontFaceHandle
{
    Face Face { get; }
}

internal sealed class FontFaceHandle : IFontFaceHandle
{
    // Keep this alive to avoid it being GC'd.
    private readonly IFontMemoryHandle _memoryHandle;
    public Face Face { get; }

    public FontFaceHandle(Face face, IFontMemoryHandle memoryHandle)
    {
        _memoryHandle = memoryHandle;
        Face = face;
    }
}

