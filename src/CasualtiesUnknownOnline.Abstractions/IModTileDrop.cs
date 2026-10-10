namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// One authored drop entry for a custom tile. This is the contract a consumer
/// reads: <see cref="IModTileDefinition.Drops"/> is a list of these, so a mod that
/// computes a drop hands over its own implementation instead of filling in the
/// framework's.
///
/// <see cref="ModTileDrop"/> is the framework's ready-made implementation: use it
/// when every value is a constant, and implement this interface when one is
/// computed. Partial customisation is composition — an implementation hands back a
/// filled default for the members it does not touch — never inheritance from the
/// data class, which stays <c>sealed</c>.
///
/// What a drop DOES with these values (how the roll clamps into the authored
/// condition range) is the framework's rule, not a member here.
/// </summary>
public interface IModTileDrop
{
	/// <summary>Item content id or vanilla item id spawned when the tile breaks.</summary>
	string ItemId { get; }

	/// <summary>Probability that this drop is spawned (0..1).</summary>
	float Chance { get; }

	/// <summary>Minimum spawned item condition (0..1).</summary>
	float MinCondition { get; }

	/// <summary>Maximum spawned item condition (0..1).</summary>
	float MaxCondition { get; }
}
