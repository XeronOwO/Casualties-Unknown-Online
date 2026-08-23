using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// The host-authoritative trader-recruit result, sent only to the revived
/// player. It carries the post-revive physiological state (a full
/// <see cref="CharacterHealthMsg"/> + limb list) so the target's local Body is
/// restored to life without running the reconnect restore machinery (no
/// inventory wipe, no position teleport — the player stays where their dead
/// body is).
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
}
