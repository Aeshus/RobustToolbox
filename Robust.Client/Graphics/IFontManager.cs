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
    IFontFaceHandle Load(Stream stream, int index = 0);
    IFontFaceHandle Load(IFontMemoryHandle memory, int index = 0);

    /// <summary>
    /// Load a specified font in a font collection.
    /// </summary>
    /// <param name="memory">Memory for the entire font collection.</param>
    /// <param name="postscriptName">The postscript name of the font to load.</param>
    IFontFaceHandle LoadWithPostscriptName(IFontMemoryHandle memory, string postscriptName);

    IFontInstanceHandle MakeInstance(IFontFaceHandle handle, int size);
    void SetFontDpi(uint fontDpi);
}
