using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Full character snapshot for session-scoped save/restore (character-data-plan):
/// the guest reports it periodically (1-2 Hz), the host keeps the latest per
/// SteamID and hands it back when the same player reconnects, so the guest can
/// rebuild its character after the game spawned a fresh default one.
/// One message serves both directions (report and restore).
/// The field set mirrors the game's own save system (SaveSystem's [JsonProperty]
/// reflection over Body and Limb, Body.cs:3779+ / Limb.cs:656+) so a restore is
/// complete — deliberately no piecemeal additions later.
/// </summary>
[ProtoContract]
public sealed class CharacterDataMsg
{
	[ProtoMember(1)]
	public CharacterSkillsMsg? Skills { get; set; }

	[ProtoMember(2)]
	public CharacterHealthMsg? Health { get; set; }

	[ProtoMember(3)]
	public List<CharacterLimbMsg> Limbs { get; set; } = [];

	[ProtoMember(4)]
	public List<CharacterItemMsg> Items { get; set; } = [];

	[ProtoMember(5)]
	public int HandSlot { get; set; } = -1; // -1 = don't touch
}
