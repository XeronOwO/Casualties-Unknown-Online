using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Host → a member target's client: apply the full heal reset to your own body now. The reset is a
/// mutation of the TARGET's body, so the target's own client applies it and re-reports its state;
/// the host never writes another player's body. The operator is named so the target's console can
/// say who reset it.
/// </summary>
[ProtoContract]
public sealed class HealResetInstructionMsg
{
	/// <summary>The player whose <c>/heal</c> caused this instruction (for the target's own console line and the log).</summary>
	[ProtoMember(1)]
	public ulong OperatorSteamId { get; set; }
}
