namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// What one delivery of a declared packet did on this copy — the router's
/// verdict, returned to the frame's own receive path so that the relay (and
/// only the relay) is the framework's next step. Every value except
/// <see cref="Applied"/> and <see cref="Relay"/> is a refusal that the
/// deciding side has already logged.
/// </summary>
internal enum ModPacketRoute
{
	/// <summary>No declaration with that packet id on this copy — dropped, logged by the router.</summary>
	UnknownPacket = 0,

	/// <summary>The declared sender policy, or a validate handler, refused the delivery.</summary>
	Refused = 1,

	/// <summary>The chain ran; the declaration relays nothing.</summary>
	Applied = 2,

	/// <summary>The chain ran on the host and the declaration says the other members run it too.</summary>
	Relay = 3,
}
