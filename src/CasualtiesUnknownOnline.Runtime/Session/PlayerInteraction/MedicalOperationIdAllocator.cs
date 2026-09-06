namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// One monotonically increasing operation-id source shared by every medical
/// operation domain (injection, shrapnel, other actions). Because all terminal
/// messages broadcast to every client and clients route by operation id, a
/// globally unique id prevents an injection session from being confused with a
/// shrapnel/other session that happens to use the same number.
/// </summary>
internal sealed class MedicalOperationIdAllocator
{
	private ulong _next = 1;

	internal ulong Next() => _next++;
}
