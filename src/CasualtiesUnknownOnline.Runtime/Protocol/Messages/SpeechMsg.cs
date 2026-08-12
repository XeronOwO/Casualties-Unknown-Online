using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// A speech bubble (the Talker domain — NetMsg 74). The bubble's text is DATA:
/// the speaking side applied its localization, random line pick and body-state
/// distortion (Talker.Talk, Talker.cs:487-576), so the message carries the
/// FINAL string — the receiver just displays it and must never re-derive it
/// (its own copy's roll would differ). A player's bubble is keyed by
/// <see cref="SpeakerSteamId"/> (the peers' clone of that player, named
/// "Character_{SteamId:X}"); a trader's bubble is keyed by
/// <see cref="TraderPosition"/> (position-keyed like the trade domain — the
/// host's trader is authoritative, the guests' traders only replay).
/// </summary>
[ProtoContract]
public sealed class SpeechMsg
{
	/// <summary>The speaking player's SteamId (0 = a trader speaks).</summary>
	[ProtoMember(1)]
	public ulong SpeakerSteamId { get; set; }

	/// <summary>The trader's position key when <see cref="SpeakerSteamId"/> == 0.</summary>
	[ProtoMember(2)]
	public NetVector2Msg? TraderPosition { get; set; }

	/// <summary>The final bubble string (localization + distortion applied on the speaking side).</summary>
	[ProtoMember(3)]
	public string Text { get; set; } = "";
}
