using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Host → target player: does YOUR body allow this medical operation to start?
/// Only the target's own client can answer it, because only its own screen holds
/// the live body — a 1 Hz report the host keeps is stale by the peer's latency,
/// so a refusal derived from it can contradict the body the target is standing
/// in. The host has already validated everything it owns (participants, the
/// operator's item facts, the claims) and parked the start request; the answer
/// rides <see cref="MedicalOperationTargetCheckAnswerMsg"/>.
/// </summary>
[ProtoContract]
public sealed class MedicalOperationTargetCheckRequestMsg
{
	/// <summary>The host's ticket for this parked request — the answer's only correlation, so a stale or forged answer can never resolve a later request.</summary>
	[ProtoMember(1)]
	public ulong RequestId { get; set; }

	/// <summary>The operator, for the target's own log line (the target judges its body, never the operator).</summary>
	[ProtoMember(2)]
	public ulong OperatorSteamId { get; set; }

	[ProtoMember(3)]
	public int LimbIndex { get; set; } = -1;

	[ProtoMember(4)]
	public MedicalOperationKind Kind { get; set; } = MedicalOperationKind.Injection;
}
