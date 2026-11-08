using System;
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

    public uint FontDpi
    {
        private get;
        set
        {
            if (field == value)
                return;

            field = value;

            foreach (var face in _loadedInstances)
            {
                face.FontDpi = value;
            }
        }
    } = 96;

    private readonly Library _library = new();

    private readonly List<FontFace> _loadedInstances = new();

    public void PostInject()
    {
        _sawmill = _logManager.GetSawmill("font");
    }

    public FontFace Load(Stream stream, int index = 0)
    {
        // Freetype directly operates on the font memory managed by us.
        // As such, the font data should be pinned in POH.
        var fontData = stream.CopyToPinnedArray();
        return Load(new ArrayFontMemoryHandle(fontData), index);
    }

    public FontFace Load(IFontMemoryHandle memory, int index = 0)
    {
        return CreateFaceHandle(() => new FaceHandle(LoadFace(memory, index), memory));
    }

    public FontFace LoadWithPostscriptName(IFontMemoryHandle memory, string postscriptName)
    {
        return CreateFaceHandle(() => new FaceHandle(LoadFaceWithPostscriptName(memory, postscriptName), memory));
    }

    private Face LoadFaceWithPostscriptName(IFontMemoryHandle memory, string postscriptName)
    {
        var numFaces = 1;

        for (var i = 0; i < numFaces; i++)
        {
            var face = LoadFace(memory, i);
            numFaces = face.FaceCount;

            if (face.GetPostscriptName() == postscriptName)
                return face;

            face.Dispose();
        }

        // Fallback, load SOMETHING.
        _sawmill.Warning($"Failed to load correct font via postscript name! {postscriptName}");
        return LoadFace(memory, 0);
    }

    private unsafe Face LoadFace(IFontMemoryHandle memory, int index)
    {
        return new Face(_library,
            (nint)memory.GetData(),
            checked((int)memory.GetDataSize()),
            index);
    }

    private FontFace CreateFaceHandle(Func<FaceHandle> faceHandle)
    {
        var handle = new FontFace(_clyde, _library, faceHandle)
        {
            FontDpi = FontDpi
        };
        _loadedInstances.Add(handle);
        return handle;
    }

    public void ClearFontCache()
    {
        foreach (var fontInstance in _loadedInstances)
        {
            fontInstance.ClearSizeData();
        }
    }
}
