using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Host → relevant clients: authoritative in-progress state for one medical
/// operation. This is NOT terminal; <see cref="MedicalOperationEndCommittedMsg"/>
/// is the only final result.
/// </summary>
[ProtoContract]
public sealed class MedicalOperationStateMsg
{
	[ProtoMember(1)]
	public ulong OperationId { get; set; }

	[ProtoMember(2)]
	public ulong OperatorSteamId { get; set; }

	[ProtoMember(3)]
	public ulong TargetSteamId { get; set; }

	[ProtoMember(4)]
	public ulong ItemInstanceId { get; set; }

	[ProtoMember(5)]
	public int LimbIndex { get; set; } = -1;

	[ProtoMember(6)]
	public float CommittedMl { get; set; }

	[ProtoMember(7)]
	public int Sequence { get; set; }

	[ProtoMember(8)]
	public CharacterItemMsg? ItemAfter { get; set; }

	[ProtoMember(9)]
	public CharacterHealthMsg? TargetHealth { get; set; }

	[ProtoMember(10)]
	public List<CharacterLimbMsg> TargetLimbs { get; set; } = [];

	/// <summary>The operation category; non-injection/shrapnel clients use it to route active local minigame cleanup/apply.</summary>
	[ProtoMember(12)]
	public MedicalOperationKind Kind { get; set; } = MedicalOperationKind.Injection;

	/// <summary>Generic action progress for Stage 3 actions (bandage consumed fraction, amputation cut, defibrillator stage).</summary>
	[ProtoMember(13)]
	public float ActionProgress { get; set; }

	/// <summary>
	/// The drained amounts this delta committed, for the TARGET's own client to
	/// apply to its own body through the game's native injection path. Every
	/// other recipient ignores it: the effect belongs to the affected side.
	/// </summary>
	[ProtoMember(14)]
	public List<LiquidStackMsg> AppliedDose { get; set; } = [];

	/// <summary>Shared shrapnel session piece state (empty for injection).</summary>
	[ProtoMember(11)]
	public List<ShrapnelPieceMsg> ShrapnelPieces { get; set; } = [];
}
