using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Guest → host: the handshake ack arrived (the third leg of the handshake,
/// TCP-style). The host marks the member Handshaken only on receiving this —
/// without it, "the host received a handshake" and "the guest received the
/// ack" are different facts, and the start gate would wait on members whose
/// connection never completed (a lost ack keeps the guest retrying forever,
/// holding the host's start gate 30 s for a member that is not even loading).
/// </summary>
[ProtoContract]
public sealed class HandshakeAckAckMsg
{
}
