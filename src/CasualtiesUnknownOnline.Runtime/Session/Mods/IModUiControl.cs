using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The narrow surface the Unity plugin needs from the mod domain for the local
/// mod UI: the list of registered mod windows, in stable discovery/registration
/// order. The plugin draws each window through its own Unity IMGUI bridge; the
/// mod domain never references Unity or the game assemblies.
/// </summary>
public interface IModUiControl
{
	/// <summary>The currently registered mod UI windows (a snapshot — safe to enumerate).</summary>
	IReadOnlyList<ModUiWindow> Windows { get; }
}
