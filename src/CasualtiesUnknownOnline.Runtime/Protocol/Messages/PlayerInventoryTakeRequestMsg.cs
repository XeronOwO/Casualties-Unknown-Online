using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Guest → host request for the "take items from another player" direct
/// interaction: the local player wants to take one carried item (identified by
/// its stable instance id) out of another in-world player's inventory. The
/// host is the cross-player authority — it validates the item against its
/// current character-data snapshots, moves the ownership record and tells the
/// two participants to apply the local body mutation. No evidence digest rides
/// the request: a carried item is already owned state, not a world-table race.
/// </summary>
[ProtoContract]
public sealed class PlayerInventoryTakeRequestMsg
{
	/// <summary>The SteamId of the player whose inventory the item is being taken from.</summary>
	[ProtoMember(1)]
	public ulong OwnerSteamId { get; set; }

	/// <summary>The stable instance id of the taken item (0 = unbound/generation-time — not takeable).</summary>
	[ProtoMember(2)]
	public ulong ItemInstanceId { get; set; }
}
