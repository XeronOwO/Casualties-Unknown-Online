using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// Typed base for item capabilities. The concrete capability decides which
/// item definitions/components it applies to and how to capture/restore its
/// own slice; the base adapts the five-surface interface to the typed state.
/// </summary>
public abstract class ItemCapabilityBase : IItemCapability
{
	public abstract string Name { get; }

	public abstract bool AppliesTo(Item item);

	public abstract CharacterItemMsg Capture(Item item);

	public abstract void Restore(Item item, CharacterItemMsg state);

	public abstract bool Equivalent(CharacterItemMsg left, CharacterItemMsg right);

	public abstract bool Validate(Item item, CharacterItemMsg state);

	public abstract object? Presentation(Item item, CharacterItemMsg state);

	object? IItemCapability.Capture(Item item) => Capture(item);

	void IItemCapability.Restore(Item item, object? state)
	{
		if (state is CharacterItemMsg typed)
		{
			Restore(item, typed);
		}
	}

	bool IItemCapability.Equivalent(object? left, object? right) =>
		left is CharacterItemMsg leftTyped && right is CharacterItemMsg rightTyped
			? Equivalent(leftTyped, rightTyped)
			: false;

	bool IItemCapability.Validate(Item item, object? state) =>
		state is CharacterItemMsg typed && Validate(item, typed);

	object? IItemCapability.Presentation(Item item, object? state) =>
		state is CharacterItemMsg typed ? Presentation(item, typed) : null;
}
