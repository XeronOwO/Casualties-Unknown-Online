using System;
using CasualtiesUnknownOnline.Runtime.Session.World;

namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>
/// The Game Adapter boundary (architecture.md §4): the only layer that knows
/// the game's private types. One implementation per game build; the Runtime
/// defines the contract, the adapter project (CUO.GameAdapter) implements it.
/// The plugin resolves this via DI and forwards ICuoService lifecycle calls.
/// </summary>
public interface IGameAdapter : IDisposable
{
	/// <summary>Probes the loaded game assembly for the types/methods this build supports.</summary>
	bool ProbeGame();

	/// <summary>Human-readable capability report for startup logs.</summary>
	string CapabilityReport { get; }

	/// <summary>Installs Harmony patches. Must be safe to fail without breaking single-player.</summary>
	bool Install();

	/// <summary>Uninstalls Harmony patches.</summary>
	void Uninstall();

	/// <summary>True while the start gate holds this player (everyone loads together; frozen + overlay).</summary>
	bool IsWaitingForReady { get; }

	/// <summary>True while the local player is in a world or one is generating — lobby switches are refused in that window (menu-only switch policy).</summary>
	bool IsInWorldOrGenerating { get; }

	/// <summary>Overlay text while the gate holds: who we are waiting for and the force-start countdown.</summary>
	string WaitingText { get; }

	/// <summary>Host side: called at run start (PreRunScript.StartRun) — captures Random.state + run settings.</summary>
	void CaptureWorldParams();

	/// <summary>Guest side: called right before world generation — applies the host's world params.</summary>
	void ApplyWorldParams(WorldStartParams parameters);

	/// <summary>
	/// The game is quitting (Unity's OnApplicationQuit — broadcast before the
	/// scene unloads). The teardown must engage BEFORE the unload: the world
	/// items' OnDestroy then fires while the session still reads as alive, and
	/// reporting each as a player-operation destroy wiped the host's world
	/// copies (#191).
	/// </summary>
	void OnApplicationQuit();
}
