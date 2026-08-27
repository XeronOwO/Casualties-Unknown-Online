namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// One item capability (battery, liquid, durability, gun, ammo, fuse,
/// cooldown, consumable, body component, etc.) exposed through the five
/// required surfaces: Capture, Restore, Equivalent, Validate, and
/// Presentation. A partial sync-only capability is not allowed by the Phase B
/// registry contract.
/// </summary>
public interface IItemCapability
{
	string Name { get; }

	bool AppliesTo(Item item);

	object? Capture(Item item);

	void Restore(Item item, object? state);

	bool Equivalent(object? left, object? right);

	bool Validate(Item item, object? state);

	object? Presentation(Item item, object? state);
}
