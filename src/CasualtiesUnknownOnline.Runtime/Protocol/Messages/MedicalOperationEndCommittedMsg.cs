using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Host → relevant clients: the single terminal result for a medical
/// operation. It carries the exact committed ml and the final authoritative
/// item/target snapshot after that commit. No update or other terminal message
/// supersedes this result.
/// </summary>
[ProtoContract]
public sealed class MedicalOperationEndCommittedMsg
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
	public MedicalOperationTerminalReason TerminalReason { get; set; }

	[ProtoMember(8)]
	public CharacterItemMsg? ItemAfter { get; set; }

	[ProtoMember(9)]
	public CharacterHealthMsg? TargetHealth { get; set; }

	[ProtoMember(10)]
	public List<CharacterLimbMsg> TargetLimbs { get; set; } = [];

	/// <summary>
	/// The drained amounts of the last unreported delta, for the TARGET's own
	/// client to apply to its own body through the game's native injection path.
	/// Earlier deltas travelled on their own non-terminal state messages, so a
	/// terminal without a final delta carries this empty.
	/// </summary>
	[ProtoMember(11)]
	public List<LiquidStackMsg> AppliedDose { get; set; } = [];

	/// <summary>The operation category; non-injection/shrapnel clients use it to route active local minigame cleanup/apply.</summary>
	[ProtoMember(13)]
	public MedicalOperationKind Kind { get; set; } = MedicalOperationKind.Injection;

	/// <summary>Generic final action progress for Stage 3 actions (bandage consumed fraction, amputation cut).</summary>
	[ProtoMember(14)]
	public float ActionProgress { get; set; }

	/// <summary>A newly-created item awarded to the operator by a removal action (splint/tourniquet). Null for all other actions.</summary>
	[ProtoMember(15)]
	public CharacterItemMsg? AwardedItem { get; set; }

	/// <summary>Final shared shrapnel piece state (empty for injection).</summary>
	[ProtoMember(12)]
	public List<ShrapnelPieceMsg> ShrapnelPieces { get; set; } = [];
}
