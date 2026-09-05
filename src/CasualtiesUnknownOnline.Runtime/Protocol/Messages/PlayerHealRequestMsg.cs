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

	private int _limbSelection;

	/// <summary>
	/// Wire representation of the selected limb. Zero means "no explicit
	/// selection" (host auto-pick). A positive value is stored as
	/// <c>limbIndex + 1</c> so limb 0 is not omitted by protobuf's default-zero
	/// rule.
	/// </summary>
	[ProtoMember(3)]
	public int LimbSelection
	{
		get => _limbSelection;
		set => _limbSelection = value;
	}

	/// <summary>
	/// The target limb chosen by the native medical UI, or -1 for the host's
	/// normal most-injured-limb selection. A non-negative value is validated
	/// against the target's snapshot and used instead of the auto pick.
	/// </summary>
	public int LimbIndex
	{
		get => _limbSelection <= 0 ? -1 : _limbSelection - 1;
		set => _limbSelection = value >= 0 ? value + 1 : 0;
	}
}
