using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.CharacterData;

/// <summary>
/// One remote player's carried/worn inventory in the read-only form the Online
/// UI can render. Kept deliberately small: the UI needs item ids, slot
/// positions, condition, favorite flag and container contents, not the full
/// wire <see cref="CharacterItemMsg"/>. The snapshot is immutable; the owning
/// cache stores one per SteamId.
/// </summary>
public sealed class RemoteInventorySnapshot
{
	private RemoteInventorySnapshot(
		IReadOnlyList<RemoteInventoryEntry> items,
		int handSlot)
	{
		Items = items;
		HandSlot = handSlot;
	}

	/// <summary>The top-level carried/worn items; container contents are projected recursively into <see cref="RemoteInventoryEntry.Contents"/>.</summary>
	public IReadOnlyList<RemoteInventoryEntry> Items { get; }

	/// <summary>The raw hand-slot wire value (0 = none in the game's save encoding).</summary>
	public int HandSlot { get; }

	public int Count => Items.Count;

	/// <summary>
	/// Project a character snapshot into the UI view. A null snapshot means the
	/// sender has no data yet; an empty item list is a valid cached state (the
	/// remote player may genuinely be carrying nothing).
	/// </summary>
	public static RemoteInventorySnapshot? From(CharacterDataMsg? data)
	{
		if (data is null)
		{
			return null;
		}

		var items = data.Items
			.Select(Project)
			.ToList();

		return new RemoteInventorySnapshot(items, data.HandSlot);
	}

	private static RemoteInventoryEntry Project(CharacterItemMsg item) =>
		new(
			item.InstanceId,
			item.ItemId,
			item.SlotIndex,
			item.Condition,
			item.Favourited,
			[.. item.Contents.Select(Project)]);

	/// <summary>Compact status-line text for the member list.</summary>
	public string ToShortString() => Count == 0 ? "no items" : $"{Count} item(s)";
}
