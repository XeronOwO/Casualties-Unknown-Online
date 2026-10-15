namespace CasualtiesUnknownOnline.Runtime.Session.HostRules;

/// <summary>
/// The announcement half of the host rules: the seam the world-entry fan-out sends
/// through, so the ordered entry group and the 60 s repair group carry the rules like
/// every other absolute table without either side owning the other's flow.
/// </summary>
public interface IHostRulesAnnouncement
{
	/// <summary>
	/// Host only: send the CURRENT absolute rule set to one member. Idempotent — the
	/// receiver replaces its whole stored set, so the entry group, the repair group and
	/// the admission edge may all send it.
	/// </summary>
	void Announce(ulong steamId);
}
