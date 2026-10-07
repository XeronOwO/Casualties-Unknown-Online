using CasualtiesUnknownOnline.Abstractions;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// The shared mod-message frame (NetMsg.ModMessage — Phase 4 Mod API). The
/// payload is opaque to the framework: the mod owns its serialization (JSON,
/// hand-written, whatever its own dependencies allow). The receiving side
/// routes by <see cref="ModId"/> to the locally-loaded mod with that id and
/// drops unknown ids with a log — the payload never reaches the game.
/// Reliable channel (the default): ordering and delivery are guaranteed while
/// the connection lives; idempotency of repeated delivery is the mod's own
/// responsibility (same discipline as the one-shot event messages).
/// </summary>
[ProtoContract]
public sealed class ModMessageMsg
{
	/// <summary>The sending mod's declared id (CuoModAttribute.Id).</summary>
	[ProtoMember(1)]
	public string ModId { get; set; } = string.Empty;

	/// <summary>The mod-owned payload (framework policy: ≤ 64 KiB, checked on both the send and the receive side).</summary>
	[ProtoMember(2)]
	public byte[] Payload { get; set; } = [];

	/// <summary>
	/// The declared packet's mod-owned id, when the frame is a declared packet
	/// (<see cref="IModPackets"/>). It is what the receiving copy routes the
	/// frame by, beside the mod id, so the identity of a mod's message is the
	/// mod's own name for it instead of a convention inside the payload.
	///
	/// EMPTY means the frame is the anonymous tunnel form
	/// (<see cref="IModNetwork"/>): one opaque payload per mod, delivered to the
	/// mod's single receive callback, which is what the framework's own status
	/// transport and the example mod's echo use. A non-empty id that the
	/// receiving copy has not declared is dropped with a log — never guessed at.
	/// </summary>
	[ProtoMember(3)]
	public string PacketId { get; set; } = string.Empty;
}
