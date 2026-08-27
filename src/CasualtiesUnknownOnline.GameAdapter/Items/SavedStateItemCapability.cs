using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// The save-shaped item state capability: condition, favourited, slot,
/// liquids, and generic component payload. This is the baseline capability
/// every item has; the more specific capabilities above it narrow their
/// AppliesTo and presentation semantics.
/// </summary>
public sealed class SavedStateItemCapability : ItemCapabilityBase
{
	public override string Name => "saved-state";

	public override bool AppliesTo(Item item) => item != null; // Unity object — ==

	public override CharacterItemMsg Capture(Item item) => ItemStateCodec.CaptureItem(item, -1);

	public override void Restore(Item item, CharacterItemMsg state)
	{
		item.condition = state.Condition;
		item.favourited = state.Favourited;
		ItemStateCodec.RestoreLiquids(item, state.Liquids);
		ItemStateCodec.RestoreComponentStates(item, state.Components);
	}

	public override bool Equivalent(CharacterItemMsg left, CharacterItemMsg right) =>
		ItemCapabilityStateComparer.SameTopLevel(left, right);

	public override bool Validate(Item item, CharacterItemMsg state) =>
		item != null && state is not null && !float.IsNaN(state.Condition); // Unity object — ==

	public override object? Presentation(Item item, CharacterItemMsg state) => state.Condition;
}
