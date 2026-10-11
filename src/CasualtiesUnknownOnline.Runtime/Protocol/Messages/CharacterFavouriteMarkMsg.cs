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
/// The item is named by CUO's authoritative instance id, the identity every
/// other seam already keys by, plus the two facts that make a stale id
/// detectable: the owner the item had when it was marked, and its definition. A
/// mark whose id later names a different owner or a different definition is
/// dropped instead of applied to whatever now holds that id; the consequence is
/// that a mark does NOT follow an item that changes hands to a third player, which
/// is the safe side of that trade (see <c>LocalFavouriteMarks.IsMarked</c>).
/// </para>
/// </summary>
[ProtoContract]
public sealed class CharacterFavouriteMarkMsg
{
	/// <summary>The SteamId of the player who owned the marked item when the mark was made.</summary>
	[ProtoMember(1)]
	public ulong OwnerSteamId { get; set; }

	/// <summary>The marked item's authoritative instance id.</summary>
	[ProtoMember(2)]
	public ulong ItemInstanceId { get; set; }

	/// <summary>The marked item's definition id — the guard against an id that now names another item.</summary>
	[ProtoMember(3)]
	public string ItemId { get; set; } = string.Empty;
}
