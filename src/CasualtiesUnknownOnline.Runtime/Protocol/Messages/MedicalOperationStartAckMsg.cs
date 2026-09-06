using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Host → operator: the authoritative verdict for a medical operation start and
/// the operation id that all subsequent updates/end/cancel messages use. A
/// rejected start carries no operation id.
/// </summary>
[ProtoContract]
public sealed class MedicalOperationStartAckMsg
{
	[ProtoMember(1)]
	public ulong OperationId { get; set; }

	[ProtoMember(2)]
	public bool Accepted { get; set; }

	[ProtoMember(3)]
	public string? RejectReason { get; set; }

	[ProtoMember(4)]
	public ulong OperatorSteamId { get; set; }

	[ProtoMember(5)]
	public ulong TargetSteamId { get; set; }

	[ProtoMember(6)]
	public ulong ItemInstanceId { get; set; }

	[ProtoMember(7)]
	public int LimbIndex { get; set; } = -1;

	[ProtoMember(8)]
	public MedicalOperationKind Kind { get; set; } = MedicalOperationKind.Injection;
}
