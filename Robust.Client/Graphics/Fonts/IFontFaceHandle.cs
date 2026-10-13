using SharpFont;

namespace Robust.Client.Graphics.Fonts;

internal sealed class FontFaceHandle
{
    // Keep this alive to avoid it being GC'd.
    // ReSharper disable once NotAccessedField.Local
    private readonly IFontMemoryHandle _memoryHandle;
    public Face Face { get; }

    public FontFaceHandle(Face face, IFontMemoryHandle memoryHandle)
    {
        _memoryHandle = memoryHandle;
        Face = face;
    }
}

