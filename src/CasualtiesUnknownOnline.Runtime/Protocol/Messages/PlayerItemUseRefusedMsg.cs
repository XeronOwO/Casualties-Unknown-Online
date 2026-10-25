using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Host → the operator whose cross-player use it refused, and why. The drag gesture's refusal used to live in the
/// host's log only, which made it indistinguishable from a gesture that silently did nothing — the user's
/// 2026-10-24 ruling is that the operator reads it on screen and hears the game's own deny sound.
///
/// <para>
/// What crosses is a KEY plus at most one argument, never the host's sentence (the user's 2026-10-25 ruling: "不只是命令，
/// 涉及到多语言的都要改"). The visible case that settled it was a Chinese client reading
/// <c>使用被拒绝：injectable medicine goes through the medical view。</c> — a Chinese frame around an English sentence the host
/// had logged. The reader's own catalogue turns the key into a whole sentence in the READER's language; the host's log keeps
/// its own English line built from the same key.
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

	/// <summary>The catalogue key of the reason — one of <c>RefusalReason.RefusalKeys</c>.</summary>
	[ProtoMember(3)]
	public string ReasonKey { get; set; } = "";

	/// <summary>The single value the key needs, or an empty string when it needs none.</summary>
	[ProtoMember(4)]
	public string ReasonArg { get; set; } = "";
}
