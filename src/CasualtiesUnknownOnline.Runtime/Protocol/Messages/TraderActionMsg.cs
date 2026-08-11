using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// A player → trader interaction — guest → host, reliable, a report of a
/// locally-executed action (the game method ran in full on the acting side:
/// player-side effects — exp, bitten limbs, the pushed ragdoll, the bought
/// item landing in the acting player's own inventory — are immediate; the
/// trader-side state change is recomputed by the host and broadcast back as
/// <see cref="TraderStateMsg"/>, a full overwrite). The host never re-runs the
/// game method for a guest (the item creation would land in the wrong
/// inventory, the random consumption would double-roll): it executes the
/// trader-side change only (TradeExecutor).
/// </summary>
[ProtoContract]
public sealed class TraderActionMsg
{
	[ProtoMember(1)]
	public TraderActionKind Action { get; set; }

	/// <summary>The trader's world position — the host locates its own trader by it (position-keyed).</summary>
	[ProtoMember(2)]
	public NetVector2Msg Position { get; set; } = new();

	/// <summary>Purchase: the stock entry's id the acting side clicked (id-keyed — the bandage insertion shifts the list, an index would land on the wrong item).</summary>
	[ProtoMember(3)]
	public string ItemId { get; set; } = "";

	/// <summary>GiveItem: the item's value (Item.GetItem(id).value — deterministic, both sides derive the same).</summary>
	[ProtoMember(4)]
	public int ItemValue { get; set; }

	/// <summary>
	/// MeetPlayer: the acting player's reputation modifier (the game reads the
	/// player's body state — INT, dirtiness, the held gun, mindWipe, brain
	/// health, happiness, hearing, bleeding; TraderScript.cs:113-137). A
	/// deterministic function of the acting player's own body, computed on the
	/// acting side — the host rolls only the random base.
	/// </summary>
	[ProtoMember(5)]
	public float ReputationOffset { get; set; }
}
