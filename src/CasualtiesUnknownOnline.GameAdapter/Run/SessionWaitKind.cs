namespace CasualtiesUnknownOnline.GameAdapter.Run;

/// <summary>
/// What a held client is waiting for, as one value rather than as a stack of
/// conditions inside a string getter. The DIRECTION of the wait is the thing the
/// old text could not say: a member waiting for the host and a host waiting for its
/// members were two branches of one expression, and a reader on screen could not
/// tell which of the two they were looking at.
/// </summary>
internal enum SessionWaitKind
{
	/// <summary>A member is waiting for the HOST to enter the world — the host is still generating the layer.</summary>
	HostLoading,

	/// <summary>The host is waiting for the members it names — the group starts together.</summary>
	MembersLoading,

	/// <summary>A member is waiting for the OTHER members; the host itself has already entered the world.</summary>
	OthersLoading,

	/// <summary>Everyone the wait was about has arrived; the run is about to start.</summary>
	Starting,
}
