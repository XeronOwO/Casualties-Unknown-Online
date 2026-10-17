namespace CasualtiesUnknownOnline.Runtime.Session.Permissions;

/// <summary>
/// The announcement half of the cheat-permission roster: the seam the world-entry
/// fan-out sends through, so the ordered entry group and the in-session repair group
/// carry the list like every other absolute session fact without either side owning
/// the other's flow. The admission edge and every grant or revoke reach the members
/// through the same implementation.
/// </summary>
public interface ICheatPermissionAnnouncement
{
	/// <summary>
	/// Host only: send the CURRENT absolute roster to one member. Idempotent — the
	/// receiver replaces its whole stored list, so the admission edge, the entry
	/// group, the repair group and a grant/revoke may all send it.
	/// </summary>
	void Announce(ulong steamId);

	/// <summary>
	/// Host only: send the roster to every member of the session — what a grant or a
	/// revoke calls, because the list governs verbs members type in a world they are
	/// already in.
	/// </summary>
	void AnnounceAll();
}
