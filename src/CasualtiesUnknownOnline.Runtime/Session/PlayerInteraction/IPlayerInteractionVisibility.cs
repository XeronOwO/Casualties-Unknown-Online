namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The game-specific line-of-sight oracle for direct player-to-player
/// interactions. The Runtime owns the interaction policy and calls this narrow
/// gateway before committing an authoritative action; the Game Adapter owns the
/// actual world query (Physics2D/ground linecast between the two players). A
/// default allow-all implementation keeps tests and non-game compositions
/// working; production replaces it with the adapter-backed gate.
/// </summary>
public interface IPlayerInteractionVisibility
{
	/// <summary>
	/// Returns true when <paramref name="observerSteamId"/> has direct line of
	/// sight to <paramref name="targetSteamId"/> in the current world. The
	/// Runtime only blocks an action on an explicitly confirmed blocker; when
	/// the adapter lacks enough evidence (no world, unknown position) it may
	/// return true so missing sync never blocks gameplay.
	/// </summary>
	bool HasLineOfSight(ulong observerSteamId, ulong targetSteamId);
}
