using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// One read-only inventory line in <see cref="IModPlayerInventory"/>. A
/// negative <see cref="SlotIndex"/> is a worn item (the game encodes wear as
/// -(limbIndex + 2)); container children appear recursively in
/// <see cref="Contents"/>.
/// </summary>
public interface IModInventoryEntry
{
	/// <summary>The framework item-instance id (0 when not yet allocated).</summary>
	ulong InstanceId { get; }

	/// <summary>The game item definition id.</summary>
	string ItemId { get; }

	/// <summary>The inventory slot index, or the negative wear encoding when worn.</summary>
	int SlotIndex { get; }

	/// <summary>The item condition (charge/durability/consumable amount).</summary>
	float Condition { get; }

	/// <summary>The item's favourite flag.</summary>
	bool Favourited { get; }

	/// <summary>The direct contents of this item when it is a container.</summary>
	IReadOnlyList<IModInventoryEntry> Contents { get; }
}
