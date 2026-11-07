namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// What this client declares about itself in its handshake: the game build it
/// runs, the CUO build identity it was deployed from, and the save-archive
/// schema it reads. The Runtime cannot read Unity's game version or the plugin
/// assembly, so the composition binds them once and both handshake legs (the
/// guest's message and the host's comparison) read this one value.
/// </summary>
public interface ILocalPeerIdentity
{
	/// <summary>The game's own version string (Unity's `Application.version`), or "unknown" before the composition binds it.</summary>
	string GameBuild { get; }

	/// <summary>The CUO build stamp (`BuildIdentity.Of`), `&lt;version&gt;+&lt;commit&gt;[-dirty]`.</summary>
	string CuoBuild { get; }

	/// <summary>The kernel/save archive schema this build reads and writes.</summary>
	int SaveSchema { get; }
}
