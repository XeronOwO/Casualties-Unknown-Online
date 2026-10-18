using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Full character snapshot for save/restore (character-data-plan, decision 178):
/// the guest reports it periodically (1-2 Hz), the host keeps the latest per
/// SteamID in memory (the world archive is what persists it) and hands it back
/// when the same player reconnects, so the guest can rebuild its character after
/// the game spawned a fresh default one.
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

	/// <summary>
	/// The local body's inventory slot count (Body.slots.Length). The cross-player
	/// interaction service uses it to pick a concrete empty slot before sending a
	/// transfer; old senders default to 0, which the service falls back to the
	/// known game minimum (3) for compatibility.
	/// </summary>
	[ProtoMember(8)]
	public int SlotCount { get; set; }

	/// <summary>
	/// The native character fields the game's own save carried for this character
	/// (<c>lastHappiness</c>, <c>caloriesConsumed</c>, <c>WoundView.cInfo</c>) —
	/// null for an old sender or a capture that could not read them, which the
	/// restore names rather than defaulting (§6). See
	/// <see cref="CharacterNativeFieldsMsg"/>.
	/// </summary>
	[ProtoMember(9)]
	public CharacterNativeFieldsMsg? NativeFields { get; set; }

	/// <summary>
	/// The favourite marks THIS player made on other players' items — the local,
	/// personal act of user ruling 2026-10-11, carried here because this record is
	/// the only store scoped to the world the mark was made in. Null for an old
	/// sender, which the restore reads as "this character brought no marks" (see
	/// <see cref="LocalFavouriteMarks.Seed"/>). See
	/// <see cref="CharacterFavouriteMarkMsg"/>.
	/// </summary>
	[ProtoMember(10)]
	public List<CharacterFavouriteMarkMsg>? FavouriteMarks { get; set; }

	/// <summary>
	/// LOCAL-ONLY mutation counter, never serialized (no <c>ProtoMember</c>): the host's stored
	/// snapshot is mutated in place by the event appliers (a bite, a limb latch, an injury), and a
	/// host clone's input map must notice that mutation even though the object reference did not
	/// change. A new report or restore is a new object and carries the counter at 0; the host's
	/// store increments it after each in-place event merge. Receiver-side objects are independent,
	/// so no wire meaning is implied.
	/// </summary>
	internal int LocalRevision { get; set; }
}
