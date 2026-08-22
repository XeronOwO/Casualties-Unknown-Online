using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Guest → host request for the "heal another player" direct interaction: the
/// local player wants to use a carried medical item on an in-world target. The
/// host is the cross-player authority — it validates both participants against
/// its authoritative character snapshots, chooses the item (or the caller's
/// exact instance), applies the healing effect to the target's saved state and
/// sends the two participants an authoritative result. An item instance id of
/// 0 means "let the host choose the first usable medical item" (the Online UI
/// Heal button does not expose a local inventory picker in this slice).
/// </summary>
[ProtoContract]
public sealed class PlayerHealRequestMsg
{
	/// <summary>The SteamId of the player to heal.</summary>
	[ProtoMember(1)]
	public ulong TargetSteamId { get; set; }

	/// <summary>The healer's item instance to consume, or 0 for host auto-select.</summary>
	[ProtoMember(2)]
	public ulong ItemInstanceId { get; set; }
}
