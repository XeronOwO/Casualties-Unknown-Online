using System;
using System.Collections.Generic;
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

	/// <summary>True when the local body currently carries at least one item from the cross-player heal profile set (Online UI only — the host re-checks authority).</summary>
	bool HasLocalHealItem();

	/// <summary>
	/// The local carried heal-profile items with wire instance ids, for the
	/// Online UI's explicit item selector. Empty when no body / no usable
	/// slot item / no instance ids are available. The host remains the
	/// authority and re-validates the requested id.
	/// </summary>
	IReadOnlyList<LocalHealItem> GetLocalHealItems();

	/// <summary>
	/// Online UI entry: the local player requests a trader recruit of a dead
	/// in-world teammate. Returns false when there is no session/world or no
	/// trader within range; the host remains the authority for the actual
	/// trade gates and the revive result.
	/// </summary>
	bool TryRequestTraderRecruit(ulong targetSteamId);

	/// <summary>
	/// Tells the Game Adapter whether the CUO Online UI modal window is open.
	/// While open, the adapter suppresses interaction with the game's
	/// background UI (custom AdaptiveButton inputs and UGUI raycasts) so clicks
	/// on the UI's non-control areas do not leak to the menu/world behind it.
	/// </summary>
	void SetOnlineUiModal(bool visible);
}
