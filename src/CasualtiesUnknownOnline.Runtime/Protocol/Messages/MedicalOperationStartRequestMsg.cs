using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Guest → host: begin a remote medical operation. The host reserves the item
/// (and optional target limb), accepts or rejects, and replies with a
/// <see cref="MedicalOperationStartAckMsg"/>. No medical state changes until
/// the host has accepted the session.
/// </summary>
[ProtoContract]
public sealed class MedicalOperationStartRequestMsg
{
	[ProtoMember(1)]
	public ulong TargetSteamId { get; set; }

	[ProtoMember(2)]
	public ulong ItemInstanceId { get; set; }

	[ProtoMember(3)]
	public int LimbIndex { get; set; } = -1;

	[ProtoMember(4)]
	public MedicalOperationKind Kind { get; set; } = MedicalOperationKind.Injection;
}
