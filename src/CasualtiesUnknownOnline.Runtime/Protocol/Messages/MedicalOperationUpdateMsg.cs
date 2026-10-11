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

	/// <summary>
	/// True when this update is an ownership/semantic transition (initial grab,
	/// release, break-grasp). Ordinary held-piece position reports are false so
	/// a stale unreliable move can never re-acquire a released piece.
	/// </summary>
	[ProtoMember(10)]
	public bool OwnershipChange { get; set; }

	// Stage 3 generic action payload. These fields are meaningful only for
	// Bandage/Dislocation/Aed/ManualDefib/Amputation operations; injection and
	// shrapnel handlers ignore them.
	[ProtoMember(11)]
	public int Action { get; set; }

	[ProtoMember(12)]
	public float Value1 { get; set; }

	[ProtoMember(13)]
	public float Value2 { get; set; }

	[ProtoMember(14)]
	public float Value3 { get; set; }

	[ProtoMember(15)]
	public bool Flag1 { get; set; }

	// Derived grip-slip window payload. Meaningful only for a shrapnel break-grasp
	// report; every other update leaves them at their defaults. See
	// ShrapnelPieceUpdate.GripSlipNonce for why the nonce travels and
	// ShrapnelGripSlipRoller for what the four values are used for.
	[ProtoMember(16)]
	public int GripSlipNonce { get; set; }

	[ProtoMember(17)]
	public float GripSlipSkin { get; set; }

	[ProtoMember(18)]
	public float GripSlipBleed { get; set; }

	[ProtoMember(19)]
	public float GripSlipPain { get; set; }

	[ProtoMember(20)]
	public float GripSlipBrainLoss { get; set; }
}
