namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// Which copies run a declared packet's chain — the second half of the
/// packet's declared policy, and the rule the host routes a member's report
/// by. The declaration is read relative to the frame's sender, and it decides
/// three things at once: whether the sender's own copy acts, whether the host
/// relays a report, and to whom.
/// </summary>
[ApiStability(ApiStabilityLevel.Experimental)]
public enum ModPacketDelivery
{
	/// <summary>
	/// The chain runs on the host's copy only. A guest's report stops there
	/// (the host applies it and relays nothing), and a host-side
	/// <see cref="IModPackets.SendToPeer"/> or <see cref="IModPackets.Broadcast"/>
	/// of such a packet is refused — no other copy may run it.
	/// </summary>
	HostOnly = 0,

	/// <summary>
	/// Every member except the sender runs the chain. The sender's own copy
	/// does not: a guest reports it and the host relays the frame to the other
	/// members after running its own chain; a host's broadcast runs on the
	/// other members and not on the host's own copy.
	/// </summary>
	EveryOtherMember = 1,

	/// <summary>
	/// The sender's own copy runs the chain too. A guest's report runs the
	/// reporter's chain before the frame leaves, then the host's chain and the
	/// relay to the other members; a host's broadcast runs on every member,
	/// the host's own copy included. A local refusal stops the send.
	/// </summary>
	EveryMember = 2,
}
