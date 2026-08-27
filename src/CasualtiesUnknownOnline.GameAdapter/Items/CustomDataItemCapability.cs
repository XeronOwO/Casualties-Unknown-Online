using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// The CustomItemBehaviour.data capability: liquidcentrifuge cooldown and
/// dynamite fuse are synthetic component fields. This is the explicit
/// replacement for the previous "partial payload sync" state; the capability
/// requires the full five surfaces like every other capability.
/// </summary>
public sealed class CustomDataItemCapability : ItemCapabilityBase
{
	public override string Name => "custom-data";

	public override bool AppliesTo(Item item) =>
		item.GetComponent<CustomItemBehaviour>() != null // Unity object — ==
		&& (item.id == CustomItemDataState.LiquidCentrifugeItemId || item.id == CustomItemDataState.DynamiteItemId);

	public override CharacterItemMsg Capture(Item item) => ItemStateCodec.CaptureItem(item, -1);

	public override void Restore(Item item, CharacterItemMsg state) => ItemStateCodec.RestoreComponentStates(item, state.Components);

	public override bool Equivalent(CharacterItemMsg left, CharacterItemMsg right) =>
		ItemCapabilityStateComparer.SameComponents(left.Components, right.Components);

	public override bool Validate(Item item, CharacterItemMsg state) =>
		AppliesTo(item)
		&& state.Components.Any(c => c.Fields.Any(f => f.Name == CustomItemDataState.CooldownFieldName || f.Name == CustomItemDataState.DynamiteFuseFieldName));

	public override object? Presentation(Item item, CharacterItemMsg state) =>
		state.Components.SelectMany(c => c.Fields)
			.Where(f => f.Name == CustomItemDataState.CooldownFieldName || f.Name == CustomItemDataState.DynamiteFuseFieldName)
			.Select(f => $"{f.Name}={f.FloatValue}{f.BoolValue}")
			.ToArray();
}
