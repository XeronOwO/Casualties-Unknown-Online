using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Host → guest: explicit "enter the world" instruction. Sent at run-start
/// entry (the moment the host clicks start, BEFORE the world params exist —
/// they are captured at the host's GenerateWorld boundary) and at handshake
/// time when the host is already in a world. Carries the entry kind so the
/// guest starts the right run immediately; the guest's generation boundary
/// then waits for the params before any random is consumed.
/// </summary>
[ProtoContract]
public sealed class WorldJoinMsg
{
	/// <summary>The host entered via StartTutorial (tutorial world) — the guest must follow via StartTutorial (it nulls runSettings itself, PreRunScript.cs:307-314).</summary>
	[ProtoMember(1)]
	public bool IsTutorial { get; set; }
}
