namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// One additive worn-sprite assignment for a specific vanilla limb on a custom
/// item. This is the contract a consumer reads:
/// <see cref="IModItemVisual.MultiWornSprites"/> is a list of these, so a mod that
/// computes a limb sprite hands over its own implementation instead of filling in
/// the framework's.
///
/// <see cref="ModItemLimbWornSprite"/> is the framework's ready-made
/// implementation: use it when every value is a constant, and implement this
/// interface when one is computed. Partial customisation is composition — an
/// implementation hands back a filled default for the members it does not touch —
/// never inheritance from the data class, which stays <c>sealed</c>.
/// </summary>
public interface IModItemLimbWornSprite
{
	/// <summary>Vanilla limb name that receives the additive sprite while the item is worn.</summary>
	string LimbName { get; }

	/// <summary>Resource path of the additive sprite shown on the named limb.</summary>
	string SpritePath { get; }

	/// <summary>Local X offset applied to the additive sprite.</summary>
	float OffsetX { get; }

	/// <summary>Local Y offset applied to the additive sprite.</summary>
	float OffsetY { get; }
}
