using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Full character snapshot for session-scoped save/restore (character-data-plan):
/// the guest reports it periodically (1-2 Hz), the host keeps the latest per
/// SteamID and hands it back when the same player reconnects, so the guest can
/// rebuild its character after the game spawned a fresh default one.
/// One message serves both directions (report and restore); the host also
/// relays a guest's report to the OTHER guests (OwnerSteamId set) so every
/// side renders that guest's clone inventory — without the relay a guest can
/// never see what another guest carries or wears.
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
	public int HandSlot { get; set; } // wire encoding: handSlot + 1, 0 = none — NOT the raw index (protobuf-net omits 0-valued ints, and hand slot 0 is a valid raw index)

	/// <summary>
	/// 0 = ownerless (host-originated restore); otherwise the SteamId of the
	/// character this snapshot belongs to — the host sets it when relaying a
	/// guest's report to the other guests (the transport's sender is the host
	/// on the receiving side, so the original owner has to ride in the payload).
	/// </summary>
	[ProtoMember(6)]
	public ulong OwnerSteamId { get; set; }

	/// <summary>
	/// The body's world position at capture — a reconnect restores the
	/// character where it LEFT, not where the fresh world placed it (observed:
	/// rejoin spawned at the landing spot instead of the disconnect spot).
	/// Null = an old-version sender or no position claim (the restore then
	/// leaves the body where it is).
	/// </summary>
	[ProtoMember(7)]
	public NetVector2Msg? Position { get; set; }
}
