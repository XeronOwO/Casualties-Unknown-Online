namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// One crafting-quality label a declaration carries: the crafting-quality id and
/// the amount it provides. This is the contract a consumer reads — the item's
/// <see cref="IModItemDefinition.Qualities"/> and the liquid's
/// <see cref="IModLiquidDefinition.Qualities"/> are lists of these — so a mod that
/// computes a label hands over its own implementation instead of filling in the
/// framework's.
///
/// <see cref="ModCraftingQuality"/> is the framework's ready-made implementation:
/// use it when every value is a constant, and implement this interface when one is
/// computed. Partial customisation is composition — an implementation hands back a
/// filled default for the members it does not touch — never inheritance from the
/// data class, which stays <c>sealed</c>.
///
/// <para>
/// The id is either a vanilla label (a bare lower-case token such as
/// <c>rippable</c>, which says the content provides that vanilla label) or a
/// mod-authored label namespaced with the content-id grammar
/// (<c>mymod:material</c>), which is what keeps two mods' vocabularies apart.
/// The game matches qualities with an ordinal string comparison, so the id is
/// used verbatim and a derived id is refused rather than normalised.
/// </para>
/// </summary>
public interface IModCraftingQuality
{
	/// <summary>The crafting-quality id.</summary>
	string Id { get; }

	/// <summary>The quality amount.</summary>
	float Amount { get; }
}
