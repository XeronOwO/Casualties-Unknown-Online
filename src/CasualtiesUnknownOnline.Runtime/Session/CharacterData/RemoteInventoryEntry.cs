using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.CharacterData;

/// <summary>One inventory line in the remote-inventory view, including recursive container contents.</summary>
public sealed record RemoteInventoryEntry(
	ulong InstanceId,
	string ItemId,
	int SlotIndex,
	float Condition,
	bool Favourited,
	IReadOnlyList<RemoteInventoryEntry> Contents)
{
	/// <summary>Number of direct items inside this entry's container (recursive contents are rendered separately).</summary>
	public int ContentsCount => Contents.Count;
}
