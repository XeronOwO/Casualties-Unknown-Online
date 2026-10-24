using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Host → the operator whose cross-player use it refused, and why. The drag gesture's refusal used to live in the
/// host's log only, which made it indistinguishable from a gesture that silently did nothing — the user's
/// 2026-10-24 ruling is that the operator reads it on screen and hears the game's own deny sound.
///
/// <para>
/// The reason travels as the SAME sentence the host logged, not a code the two sides would have to keep in step: one
/// wording for one refusal is what keeps both ends agreeing (`PlayerInventoryIntentRefusedMsg` carries its own the
/// same way).
/// </para>
/// </summary>
[ProtoContract]
public sealed class PlayerItemUseRefusedMsg
{
	/// <summary>The body the use named; 0 when the request named none.</summary>
	[ProtoMember(1)]
	public ulong TargetSteamId { get; set; }

	/// <summary>The item the use named; 0 when it auto-selected.</summary>
	[ProtoMember(2)]
	public ulong ItemInstanceId { get; set; }

	/// <summary>Why it was refused, in the words the host logged.</summary>
	[ProtoMember(3)]
	public string Reason { get; set; } = "";
}
