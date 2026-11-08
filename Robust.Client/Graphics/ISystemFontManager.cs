using System;
using System.Collections.Generic;
using System.Globalization;
using Robust.Client.Graphics.Fonts;

namespace Robust.Client.Graphics;

/// <summary>
/// Provides access to fonts installed on the user's operating system.
/// </summary>
/// <remarks>
/// <para>
/// Different operating systems ship different fonts, so you should generally not rely on any one
/// specific font being available. This system is primarily provided for allowing user preference.
/// </para>
/// </remarks>
/// <seealso cref="ISystemFontFace"/>
[NotContentImplementable]
public interface ISystemFontManager
{
    /// <summary>
    /// Whether access to system fonts is currently supported on this platform.
    /// </summary>
    bool IsSupported { get; }

    /// <summary>
    /// The list of font face available from the operating system.
    /// </summary>
    IEnumerable<ISystemFontFace> SystemFontFaces { get; }
}

/// <summary>
/// A single font face, provided by the user's operating system.
/// </summary>
/// <seealso cref="ISystemFontManager"/>
[NotContentImplementable]
[Obsolete("Use IFontFace directly")]
public interface ISystemFontFace : IFontFace;

/// <summary>
/// Engine-internal API for <see cref="ISystemFontManager"/>.
/// </summary>
internal interface ISystemFontManagerInternal : ISystemFontManager
{
    void Initialize();
    void Shutdown();
}
