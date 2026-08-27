using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// The gun capability. Gun persistent state (chamber, magazine, safety,
/// racked) rides the generic component payload; this capability makes the
/// surface explicit and verifies that the GunScript component is actually
/// present in the captured state.
/// </summary>
public sealed class GunItemCapability : ItemCapabilityBase
{
	public override string Name => "gun";

	public override bool AppliesTo(Item item) => item.GetComponent<GunScript>() != null; // Unity object — ==

	public override CharacterItemMsg Capture(Item item) => ItemStateCodec.CaptureItem(item, -1);

	public override void Restore(Item item, CharacterItemMsg state) => ItemStateCodec.RestoreComponentStates(item, state.Components);

	public override bool Equivalent(CharacterItemMsg left, CharacterItemMsg right) =>
		ItemCapabilityStateComparer.SameComponents(left.Components, right.Components);

	public override bool Validate(Item item, CharacterItemMsg state) =>
		AppliesTo(item) && state.Components.Any(c => c.TypeName == nameof(GunScript));

	public override object? Presentation(Item item, CharacterItemMsg state) =>
		state.Components.FirstOrDefault(c => c.TypeName == nameof(GunScript))?.Fields.Count ?? 0;
}
