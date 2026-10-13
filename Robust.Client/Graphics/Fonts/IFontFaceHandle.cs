using System.Collections.Generic;
using System.Text;
using SharpFont;

namespace Robust.Client.Graphics.Fonts;

internal sealed class FontFaceHandle(IClyde clyde, Library library, Face face, IFontMemoryHandle memoryHandle)
{
    // Keep this alive to avoid it being GC'd.
    // ReSharper disable once NotAccessedField.Local
    private readonly IFontMemoryHandle _memoryHandle = memoryHandle;
    private readonly Dictionary<int, FontInstanceHandle> _cachedInstances = new();
    private readonly Dictionary<Rune, uint> _glyphIndexCache = new();

    internal Face Face { get; } = face;

    public uint GetGlyphIndex(Rune chr)
    {
        if (_glyphIndexCache.TryGetValue(chr, out var glyph))
        {
            return glyph;
        }

        // Check FreeType to see if it exists.
        var index = Face.GetCharIndex((uint)chr.Value);

        _glyphIndexCache.Add(chr, index);

        return index;
    }

    public FontInstanceHandle GetInstance(int size)
    {
        if (_cachedInstances.TryGetValue(size, out var instance))
            return instance;

        instance = new FontInstanceHandle(clyde, library, this, size);
        instance.FontDpi = FontDpi;
        _cachedInstances.Add(size, instance);
        return instance;
    }

    public void ClearSizeData()
    {
        foreach (var handle in _cachedInstances.Values)
        {
            handle.ClearSizeData();
        }
    }

    public uint FontDpi
    {
        get;
        set
        {
            if (field == value)
                return;

            foreach (var handle in _cachedInstances.Values)
            {
                handle.FontDpi = value;
            }

            field = value;
        }
    }

}

