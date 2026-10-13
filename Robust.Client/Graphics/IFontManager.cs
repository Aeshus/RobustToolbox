using System.IO;
using Robust.Client.Graphics.Fonts;

namespace Robust.Client.Graphics;

[NotContentImplementable]
public interface IFontManager
{
    public void ClearFontCache();
}

internal interface IFontManagerInternal : IFontManager
{
    FontFaceHandle Load(Stream stream, int index = 0);
    FontFaceHandle Load(IFontMemoryHandle memory, int index = 0);

    /// <summary>
    /// Load a specified font in a font collection.
    /// </summary>
    /// <param name="memory">Memory for the entire font collection.</param>
    /// <param name="postscriptName">The postscript name of the font to load.</param>
    FontFaceHandle LoadWithPostscriptName(IFontMemoryHandle memory, string postscriptName);

    FontInstanceHandle MakeInstance(FontFaceHandle handle, int size);
    void SetFontDpi(uint fontDpi);
}
