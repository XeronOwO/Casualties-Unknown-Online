using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Host → a member target's client: apply one body operation to your own body now — the full heal
/// reset or the kill (<see cref="Operation"/>). Either way it is a mutation of the TARGET's body, so
/// the target's own client applies it and re-reports its state; the host never writes another
/// player's body. The operator is named so the target's console can say who acted on it.
/// </summary>
[ProtoContract]
public sealed class HealResetInstructionMsg
{
	/// <summary>The player whose verb caused this instruction (for the target's own console line and the log).</summary>
	[ProtoMember(1)]
	public ulong OperatorSteamId { get; set; }

	/// <summary>
	/// The host's ticket for this instruction. The target's <see cref="HealResetAppliedMsg"/> echoes
	/// it, and the host writes that result only while this exact instruction is outstanding for that
	/// member — a result the host never asked for writes nothing.
	/// </summary>
	[ProtoMember(2)]
	public ulong InstructionId { get; set; }

	/// <summary>Which body operation to apply (<see cref="BodyOperation"/>); 0 is refused by the target.</summary>
	[ProtoMember(3)]
	public BodyOperation Operation { get; set; }
}
