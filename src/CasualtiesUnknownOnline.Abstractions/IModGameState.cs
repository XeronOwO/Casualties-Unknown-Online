namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The read-only game-state projection available to a synchronized/authoritative
/// mod (Phase 4 Mod API). This surface exposes framework-held state that is
/// already arriving over the sync streams—never Unity objects, never live
/// game-assembly types—so a mod can build presentation/coordination logic
/// without touching the Game Adapter.
///
/// Reading requires <see cref="ModPermission.ReadGameState"/>: nothing is
/// implicit. The view is a point-in-time snapshot at the moment of the call;
/// a mod that needs continuous updates re-reads on its own cadence (for example
/// in <see cref="ICuoService.Update"/>).
/// </summary>
public interface IModGameState
{
	/// <summary>
	/// True when this mod copy declares <see cref="ModPermission.ReadGameState"/>.
	/// Every read method also checks and logs this before acting.
	/// </summary>
	bool CanRead { get; }

	/// <summary>
	/// Try to read the latest known player-state projection for one SteamId.
	/// Returns false when the mod lacks <see cref="ModPermission.ReadGameState"/>
	/// or when no character-data snapshot has arrived for that player yet.
	/// </summary>
	bool TryGetPlayer(ulong steamId, out IModPlayerState player);
}
