using CasualtiesUnknownOnline.Runtime.Protocol;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// One refusal a handshake can answer with: the dimension that failed, the
/// player-facing detail (a mod id for the mod dimensions, empty for the identity
/// dimensions) and the two values as each side declared them. It is a value —
/// the host sends it on the wire (`HandshakeRefusedMsg`) and the guest renders
/// the line from its own catalogue, so neither side owns the other's text.
/// </summary>
public sealed record HandshakeRefusal(HandshakeDimension Dimension, string Detail, string HostValue, string GuestValue);
