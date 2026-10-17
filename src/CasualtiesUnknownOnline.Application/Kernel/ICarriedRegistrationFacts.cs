namespace CasualtiesUnknownOnline.Application.Kernel;

/// <summary>
/// The one carried-record fact the replication surface has to look up: whether this
/// host has judged a member's carried-set statement yet.
///
/// <para>
/// A member's carried items are created on this host by that member's ABSOLUTE
/// carried-inventory report (<c>NetMsg.CarriedInventory</c>): the ids are the
/// member's own, and the report is re-stated on a cadence for the session's whole
/// life. Until the first one arrives the host holds no claim at all about what that
/// member carries — the documented window in which a swallowed registration is
/// healed by treating the member's first item report as the creation itself.
/// </para>
///
/// <para>
/// The fact lives outside this layer for the ordinary reason: the report is received
/// by the Runtime's item coordination and the record is session state, so this layer
/// declares the question and the Runtime answers it. It is deliberately narrower than
/// the record: a caller may ask whether the statement was judged, never what it held.
/// </para>
/// </summary>
public interface ICarriedRegistrationFacts
{
	/// <summary>This host has judged a carried-set statement from that member — the creation claim for what it carries is on record.</summary>
	bool HasJudgedCarriedSet(ulong member);
}
