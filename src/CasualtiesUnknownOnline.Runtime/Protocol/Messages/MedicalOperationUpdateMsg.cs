using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Operator → host: one accepted incremental delta for a medical operation.
/// For injection the payload is the newly delivered ml; for shrapnel the same
/// message carries the piece-index/position/ownership report. An update is
/// never a terminal commit.
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

	// Shrapnel-specific fields. They are meaningful only when the operation is
	// a shared shrapnel session; injection handlers ignore them.
	[ProtoMember(4)]
	public int PieceIndex { get; set; }

	[ProtoMember(5)]
	public float X { get; set; }

	[ProtoMember(6)]
	public float Y { get; set; }

	[ProtoMember(7)]
	public bool Grabbed { get; set; }

	[ProtoMember(8)]
	public bool Released { get; set; }

	[ProtoMember(9)]
	public bool BreakGrasp { get; set; }
}
