namespace CasualtiesUnknownOnline.GameAdapter.Content;

/// <summary>
/// The Game Adapter half of the crafting-quality vocabulary: a content provider
/// that can say whether an accepted definition of its own declares a label with
/// an amount a recipe can reach. The recipe provider asks the sources in the
/// ingredient's own direction BEFORE it falls back to the vanilla tables, so its
/// verdict does not depend on where the pump happens to stand: the item provider
/// is registered before the recipe provider and its items are already in the
/// table that frame, while the liquid provider is registered AFTER it, so a
/// liquid bound in the same frame is not in <c>Liquids.Registry</c> yet and
/// reading the tables alone would refuse a recipe that uses the label its sibling
/// provider had just declared.
///
/// <para>
/// It sits in the Game Adapter, not in Abstractions: the labels live on game
/// types, and the Runtime's publisher-agnostic content binder must stay unaware
/// of them. Like every Game Adapter type it is an implementation a mod may patch
/// but is never promised (see <c>docs/en/reference/modification-policy.md</c>).
/// </para>
/// </summary>
public interface ICraftingQualitySource
{
	/// <summary>
	/// The content kind whose vocabulary this source feeds —
	/// <c>ModContentKind.Item</c> or <c>ModContentKind.Liquid</c>. The game's
	/// matcher is direction-selected (an item ingredient is matched against
	/// <c>item.Stats.qualities</c>, a liquid one against the liquid type's
	/// qualities), and the two vanilla vocabularies do not overlap, so a source may
	/// only answer for its own direction.
	/// </summary>
	string Kind { get; }

	/// <summary>
	/// True when a definition this provider ACCEPTED declares
	/// <paramref name="qualityId"/> with an amount that can satisfy
	/// <paramref name="requiredAmount"/>; a requirement of <c>0</c> asks only for
	/// presence (the liquid direction, whose amounts scale with the volume in a
	/// container).
	///
	/// <para>
	/// Acceptance is the test, not materialization: a definition whose id collides
	/// with an entry that is already in the game table is accepted and never
	/// injected, so its labels are counted here although nothing carries them —
	/// the collision is reported by the provider's own warning, and the ticket
	/// records the resulting dead recipe as a limit.
	/// </para>
	/// </summary>
	bool ProvidesQuality(string qualityId, float requiredAmount);
}
