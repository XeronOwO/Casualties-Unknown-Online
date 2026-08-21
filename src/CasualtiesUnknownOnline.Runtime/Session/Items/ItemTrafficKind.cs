namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// The high-frequency item-domain message families the traffic observer counts.
/// One logical send operation is counted once, not once per recipient — the
/// metric answers "how many item facts are being produced", not "how many
/// transport frames hit the wire".
/// </summary>
internal enum ItemTrafficKind
{
	Spawn,
	Drop,
	Move,
	Destroy,
	Pickup,
}
