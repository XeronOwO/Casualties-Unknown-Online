namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// The allowed transport direction of a protocol message. One-way messages are
/// rejected when they arrive at the wrong role; bidirectional messages are valid
/// at both roles (the actual meaning is decided by the handler's role branch).
/// This is the single protocol-level classification used by the receiver.
/// </summary>
public enum NetMessageDirection
{
	/// <summary>Guest → host only.</summary>
	GuestToHost,

	/// <summary>Host → guest only.</summary>
	HostToGuest,

	/// <summary>Valid in both directions (report up / relay down share one id).</summary>
	Bidirectional,
}
