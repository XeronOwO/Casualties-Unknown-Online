using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// The liquid-container capability. The liquid stacks are synced through the
/// same save-shaped state; this capability is the explicit contract that the
/// WaterContainerItem surface is not a partial sync path.
/// </summary>
public sealed class LiquidItemCapability : ItemCapabilityBase
{
	public override string Name => "liquid";

	public override bool AppliesTo(Item item) => item.GetComponent<WaterContainerItem>() != null; // Unity object — ==

	public override CharacterItemMsg Capture(Item item) => ItemStateCodec.CaptureItem(item, -1);

	public override void Restore(Item item, CharacterItemMsg state) => ItemStateCodec.RestoreLiquids(item, state.Liquids);

	public override bool Equivalent(CharacterItemMsg left, CharacterItemMsg right) =>
		ItemCapabilityStateComparer.SameLiquids(left.Liquids, right.Liquids);

	public override bool Validate(Item item, CharacterItemMsg state) =>
		AppliesTo(item) && state is not null;

	public override object? Presentation(Item item, CharacterItemMsg state) => state.Liquids.Count;
}
