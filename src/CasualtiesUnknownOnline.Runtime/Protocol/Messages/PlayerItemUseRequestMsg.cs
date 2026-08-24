using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Guest → host request for the "use a carried consumable on another player"
/// direct interaction: the local player wants to give/feed/drink a carried
/// item to an in-world target. The host is the cross-player authority — it
/// validates both participants against its authoritative character snapshots,
/// chooses the item (or the caller's exact instance), consumes/updates it,
/// applies the target-side body effect to the saved state and sends the two
/// participants an authoritative result. An item instance id of 0 means "let
/// the host choose the first usable consumable" (the Online UI auto button).
/// </summary>
[ProtoContract]
public sealed class PlayerItemUseRequestMsg
{
	/// <summary>The SteamId of the player receiving the item use.</summary>
	[ProtoMember(1)]
	public ulong TargetSteamId { get; set; }

	/// <summary>The acting player's item instance to consume, or 0 for host auto-select.</summary>
	[ProtoMember(2)]
	public ulong ItemInstanceId { get; set; }
}
