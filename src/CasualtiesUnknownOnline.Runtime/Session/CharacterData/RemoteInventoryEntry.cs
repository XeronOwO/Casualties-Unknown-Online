namespace CasualtiesUnknownOnline.Runtime.Session.CharacterData;

/// <summary>One inventory line in the remote-inventory view.</summary>
public sealed record RemoteInventoryEntry(
	ulong InstanceId,
	string ItemId,
	int SlotIndex,
	float Condition,
	bool Favourited,
	int ContentsCount);
