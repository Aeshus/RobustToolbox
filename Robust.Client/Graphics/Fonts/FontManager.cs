using System.Collections.Generic;
using System.IO;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Utility;
using SharpFont;

namespace Robust.Client.Graphics.Fonts;

internal sealed partial class FontManager : IFontManagerInternal, IPostInjectInit
{
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private ILogManager _logManager = default!;

    private ISawmill _sawmill = default!;

    private uint _baseFontDpi = 96;

    private readonly Library _library = new();

    private readonly List<FontFaceHandle> _loadedInstances = new();

    public void PostInject()
    {
        _sawmill = _logManager.GetSawmill("font");
    }

    public FontFaceHandle Load(Stream stream, int index = 0)
    {
        // Freetype directly operates on the font memory managed by us.
        // As such, the font data should be pinned in POH.
        var fontData = stream.CopyToPinnedArray();
        return Load(new ArrayFontMemoryHandle(fontData), index);
    }

    public FontFaceHandle Load(IFontMemoryHandle memory, int index = 0)
    {
        return CreateFaceHandle(LoadFace(memory, 0), memory);
    }

    public FontFaceHandle LoadWithPostscriptName(IFontMemoryHandle memory, string postscriptName)
    {
        var numFaces = 1;

        for (var i = 0; i < numFaces; i++)
        {
            var face = LoadFace(memory, i);
            numFaces = face.FaceCount;

            if (face.GetPostscriptName() == postscriptName)
                return CreateFaceHandle(face, memory);

            face.Dispose();
        }

        // Fallback, load SOMETHING.
        _sawmill.Warning($"Failed to load correct font via postscript name! {postscriptName}");
        return CreateFaceHandle(LoadFace(memory, 0), memory);
    }

    private unsafe Face LoadFace(IFontMemoryHandle memory, int index)
    {
        return new Face(_library,
            (nint)memory.GetData(),
            checked((int)memory.GetDataSize()),
            index);
    }

    private FontFaceHandle CreateFaceHandle(Face face, IFontMemoryHandle memoryHandle)
    {
        var handle = new FontFaceHandle(_clyde, _library, face, memoryHandle);
        _loadedInstances.Add(handle);
        return handle;
    }

    void IFontManagerInternal.SetFontDpi(uint fontDpi)
    {
        _baseFontDpi = fontDpi;
    }

    public void ClearFontCache()
    {
        foreach (var fontInstance in _loadedInstances)
        {
            fontInstance.ClearSizeData();
        }
    }
}
