using System;
using CasualtiesUnknownOnline.Abstractions;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// One framework-owned mod UI window entry: which mod registered it, its local
/// id/title, and the per-frame draw callback. The plugin reads this list and
/// invokes <see cref="Draw"/> with its Unity-backed <see cref="IModUiWindow"/>.
/// </summary>
public sealed record ModUiWindow(string ModId, string Id, string Title, Action<IModUiWindow> Draw);
