using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The read-only carried/worn inventory projection carried by
/// <see cref="IModPlayerState"/>. Container contents are projected recursively
/// into <see cref="IModInventoryEntry.Contents"/>, matching the same
/// character-data stream the clone renderer uses.
/// </summary>
public interface IModPlayerInventory
{
	/// <summary>The top-level carried/worn items in snapshot order.</summary>
	IReadOnlyList<IModInventoryEntry> Items { get; }

	/// <summary>The raw hand-slot wire value (0 = none in the game's save encoding).</summary>
	int HandSlot { get; }

	/// <summary>The number of top-level carried/worn items.</summary>
	int Count { get; }
}
