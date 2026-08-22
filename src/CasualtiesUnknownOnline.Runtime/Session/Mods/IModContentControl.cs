using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The control surface other CUO layers use to read the mod content registry
/// (the same abstract-extraction pattern as <see cref="IModUiControl"/>): the
/// plugin or a future native-content consumer can enumerate every mod's
/// registered definitions without reaching into ModService's private state.
/// This surface is read-only by design — mods register through
/// <see cref="IModContent"/>.
/// </summary>
public interface IModContentControl
{
	/// <summary>A snapshot of every mod's registered content definitions (copy — safe to hold).</summary>
	IReadOnlyList<ModContentRegistration> Entries { get; }
}
