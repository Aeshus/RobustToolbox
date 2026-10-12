using System;

namespace Robust.Client.Graphics.Fonts;

internal unsafe interface IFontMemoryHandle : IDisposable
{
    byte* GetData();
    nint GetDataSize();
}