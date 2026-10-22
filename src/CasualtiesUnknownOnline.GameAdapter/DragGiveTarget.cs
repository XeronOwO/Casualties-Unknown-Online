namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The player whose display proxy a local release pointed at, what the pointer was on, and the operands a
/// give-direction intent needs: the item shown there (0 for a body slot), and the slot index of the ring
/// that was hit (-1 when the pointer was on a container). The description stays for the refusal line, which
/// names both ends whether or not the release turned out to be classifiable.
/// </summary>
internal readonly record struct DragGiveTarget(
	ulong OwnerSteamId,
	string Description,
	ulong TargetItemInstanceId = 0,
	int TargetSlotIndex = -1);
