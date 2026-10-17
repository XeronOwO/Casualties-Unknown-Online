namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// What one item-data target addresses: the bodies a player selector named (the
/// caller's own body when no selector was given), one body slot of those bodies, the
/// item the pointer is over, or one item by its CUO instance id.
/// </summary>
internal enum ItemDataQueryKind
{
	Bodies,
	Slot,
	Cursor,
	InstanceId,
}
