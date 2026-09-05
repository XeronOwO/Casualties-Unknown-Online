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
