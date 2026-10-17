namespace CasualtiesUnknownOnline.GameState.Domains.Players;

/// <summary>
/// Where a player-inventory transfer's item lands. It is the destination LADDER's own vocabulary
/// (the user's ruling of 2026-10-17): a body slot, a container the body carries or wears, or a
/// drop at the recipient's position. The value travels with the transfer fact so the receiving
/// client applies the rung the host chose instead of re-deciding it.
/// </summary>
public enum PlayerInventoryDestination
{
	/// <summary>A slot of the recipient's own body (the default, and every transfer that names no container).</summary>
	BodySlot = 0,

	/// <summary>The contents of a container the recipient carries or wears; the container is the transfer's parent item id.</summary>
	Container = 1,

	/// <summary>A world drop at the recipient's own position — the last rung, when no slot and no container can take the item.</summary>
	Drop = 2,
}
