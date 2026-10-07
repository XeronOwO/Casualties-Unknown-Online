namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// What one <see cref="ModPacketHandler"/> sees: the packet's identity, the
/// stage it runs in, the sender and the opaque payload. The context carries no
/// cross-handler state on purpose — a packet's whole input rides its payload,
/// and a mod that needs to remember something between two stages keeps it in
/// its own object. <see cref="Payload"/> is this delivery's OWN copy: a handler
/// may rewrite it and the later handlers of the same delivery see the rewrite,
/// while the relayed frame (and the frame a local run's send carries) keeps the
/// bytes that arrived.
/// </summary>
[ApiStability(ApiStabilityLevel.Experimental)]
public interface IModPacketContext
{
	/// <summary>The declared packet id this delivery was routed by.</summary>
	string PacketId { get; }

	/// <summary>The stage the running handler was declared in.</summary>
	ModPacketStage Stage { get; }

	/// <summary>
	/// The frame's sender — the local id for a locally-originated copy. A
	/// DIRECTED or broadcast frame from the host carries the host, and a RELAYED
	/// report carries the relaying host too, not the member that reported it: a
	/// guest's chain cannot see the original reporter, and a mod that needs it
	/// puts it in its own payload. A
	/// <see cref="ModPacketDelivery.EveryMember"/> reporter sees its own id in
	/// its own run, which is the only place the reporter's identity is visible.
	/// </summary>
	ulong SenderSteamId { get; }

	/// <summary>True when this copy runs on the session's host.</summary>
	bool IsHost { get; }

	/// <summary>
	/// The mod-owned payload, exactly as it arrived (or as it was passed to the
	/// send call for a local run). The framework never interprets it. The array
	/// belongs to this delivery: the delivery's own handlers share it, so a
	/// rewrite is visible to the rest of the chain and to nothing else — the
	/// frame the host relays carries the bytes it received.
	/// </summary>
	byte[] Payload { get; }

	/// <summary>
	/// Refuse this delivery, with a reason that reaches the log. Only
	/// <see cref="ModPacketStage.Validate"/> may refuse: the first refusal ends
	/// the delivery — no later handler runs and the host relays nothing.
	/// Calling it in a later stage is a logged no-op (the packet has already
	/// been applied).
	/// </summary>
	void Refuse(string reason);
}
