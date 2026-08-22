namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The read-only, immutable player-state projection a mod receives through
/// <see cref="IModGameState"/>. It combines the session's in-world presence
/// with the latest projected character snapshots (vitals and carried/worn
/// inventory). Null vitals/inventory mean that particular half has not arrived
/// for that player yet; the other half may still be available.
/// </summary>
public interface IModPlayerState
{
	/// <summary>The Steam 64-bit id of the player this snapshot describes.</summary>
	ulong SteamId { get; }

	/// <summary>True when this side's session knows the player is currently in the world.</summary>
	bool InWorld { get; }

	/// <summary>The latest projected vitals, or null when no health block has arrived.</summary>
	IModPlayerVitals? Vitals { get; }

	/// <summary>The latest projected carried/worn inventory, or null when no item snapshot has arrived.</summary>
	IModPlayerInventory? Inventory { get; }
}
