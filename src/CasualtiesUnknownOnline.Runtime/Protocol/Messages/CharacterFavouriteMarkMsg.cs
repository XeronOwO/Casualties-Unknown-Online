using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// One favourite mark the player whose character carries this entry made on an
/// item SOMEBODY ELSE owns. The mark is a local, personal act (user ruling
/// 2026-10-11): it never travels as an action on the marked item, and no other
/// client interprets it — it rides the marker's OWN character record because that
/// record is the only store in CUO that is scoped to the world the mark was made
/// in, and that record lives in <c>characters/&lt;playerKey&gt;.json</c> inside
/// that world's archive. (The host stores it as it stores the rest of a character
/// and the peers carry it as they carry any character snapshot; neither reads this
/// list, and the marked item's owner is told nothing about it.)
/// <para>
/// The item is named by CUO's authoritative instance id, the identity every other
/// seam already keys by, plus the two facts that make a stale id detectable: the
/// owner the item was last seen with, and its definition. The DEFINITION is the
/// guard — a mark whose id now carries an item of another definition is dropped
/// instead of applied to whatever inherited that id — while the owner is context the
/// mark follows: an item handed from one player to another is still the item this
/// player starred, and it has to still be marked when it reaches this player.
/// </para>
/// </summary>
[ProtoContract]
public sealed class CharacterFavouriteMarkMsg
{
	/// <summary>The SteamId of the player the marked item was last seen with — context the mark follows, refreshed whenever the item changes hands.</summary>
	[ProtoMember(1)]
	public ulong OwnerSteamId { get; set; }

	/// <summary>The marked item's authoritative instance id.</summary>
	[ProtoMember(2)]
	public ulong ItemInstanceId { get; set; }

	/// <summary>The marked item's definition id — the guard against an id that now names another item.</summary>
	[ProtoMember(3)]
	public string ItemId { get; set; } = string.Empty;
}
