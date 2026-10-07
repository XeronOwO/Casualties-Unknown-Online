namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// Who may send a declared packet — the first half of the packet's declared
/// policy, enforced twice: at the sender (a wrong-role send is refused before
/// it leaves, with a log) and again at the host when a member's frame arrives.
/// A guest never judges it on an inbound frame: the only peer a guest hears
/// from is the host, and the host already judged the report before relaying it.
/// </summary>
[ApiStability(ApiStabilityLevel.Experimental)]
public enum ModPacketSender
{
	/// <summary>Any member may send it: a guest reports it to the host, the host sends it to a member or broadcasts it.</summary>
	AnyMember = 0,

	/// <summary>
	/// Only a guest may start it. On the host, a frame the host itself
	/// originated is refused for this packet; a member's report is accepted.
	/// </summary>
	GuestOnly = 1,

	/// <summary>
	/// Only the host may start it. On the host, a member's report of this
	/// packet is refused.
	/// </summary>
	HostOnly = 2,
}
