using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// The host-authoritative trader-recruit result, sent only to the revived
/// player. It carries the post-revive physiological state (a full
/// <see cref="CharacterHealthMsg"/> + limb list) so the target's local Body is
/// restored to life without running the reconnect restore machinery (no
/// inventory wipe). The revived player is PLACED at the trader that performed the
/// service: the trader is where the service happens, so that is where the
/// teammate stands up — not the spot their body happened to fall, which from the
/// operator's side looks like the teammate coming back somewhere else. It also
/// carries optional random trader-stock bonus items granted as part of the
/// recruit, each in its host-chosen slot.
/// </summary>
[ProtoContract]
public sealed class TraderRecruitResultMsg
{
	[ProtoMember(1)]
	public ulong TargetSteamId { get; set; }

	[ProtoMember(2)]
	public CharacterHealthMsg? Health { get; set; }

	[ProtoMember(3)]
	public List<CharacterLimbMsg> Limbs { get; set; } = [];

	[ProtoMember(4)]
	public List<CharacterItemMsg> Items { get; set; } = [];

	/// <summary>The trader that performed the service — where the revived body stands up.</summary>
	[ProtoMember(5)]
	public float TraderX { get; set; }

	[ProtoMember(6)]
	public float TraderY { get; set; }
}
