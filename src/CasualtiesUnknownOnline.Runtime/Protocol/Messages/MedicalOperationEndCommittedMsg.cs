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

	[ProtoMember(11)]
	public List<TimedBodyEffectMsg> TimedBodyEffects { get; set; } = [];

	/// <summary>Final shared shrapnel piece state (empty for injection).</summary>
	[ProtoMember(12)]
	public List<ShrapnelPieceMsg> ShrapnelPieces { get; set; } = [];
}
