using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Operator → host: one accepted incremental ml delta since the previous
/// update. The host applies the delta to the authoritative snapshot and
/// broadcasts <see cref="MedicalOperationStateMsg"/>; an update is never a
/// terminal commit.
/// </summary>
[ProtoContract]
public sealed class MedicalOperationUpdateMsg
{
	[ProtoMember(1)]
	public ulong OperationId { get; set; }

	[ProtoMember(2)]
	public float DeltaMl { get; set; }

	[ProtoMember(3)]
	public int Sequence { get; set; }
}
