using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Host → participant(s) authoritative result of a cross-player consumable use.
/// One operation = one message: the acting player learns whether its item was
/// consumed, destroyed or had its liquid stack drained, and the target receives
/// the host-computed post-use body state so its local body can apply the exact
/// same effect inside a RemoteApply scope. The target re-reports its character
/// snapshot immediately, so the host save and every peer clone converge without
/// waiting for the next 1 Hz tick.
/// </summary>
[ProtoContract]
public sealed class PlayerItemUseResultMsg
{
	/// <summary>The player whose carried consumable was used.</summary>
	[ProtoMember(1)]
	public ulong UserSteamId { get; set; }

	/// <summary>The player who received the consumable's effect.</summary>
	[ProtoMember(2)]
	public ulong TargetSteamId { get; set; }

	/// <summary>The consumed item's stable instance id (0 = auto-select/unknown).</summary>
	[ProtoMember(3)]
	public ulong ItemInstanceId { get; set; }

	/// <summary>True when the item's condition reached zero and the item is destroyed.</summary>
	[ProtoMember(4)]
	public bool ItemDestroyed { get; set; }

	/// <summary>The item's post-use wire state (condition/liquids), or null when destroyed.</summary>
	[ProtoMember(5)]
	public CharacterItemMsg? ItemAfter { get; set; }

	/// <summary>The target's post-use body health state.</summary>
	[ProtoMember(6)]
	public CharacterHealthMsg? Health { get; set; }

	/// <summary>The target's post-use full limb state (unchanged for consumables, included for complete restore symmetry).</summary>
	[ProtoMember(7)]
	public List<CharacterLimbMsg> Limbs { get; set; } = [];

	/// <summary>The wearable item placed on the target's body, or null for consumable/tool uses. When set, the acting player's local item is removed and the target's local body wears this exact wire item.</summary>
	[ProtoMember(8)]
	public CharacterItemMsg? WornItem { get; set; }

	/// <summary>
	/// Timed limb ticks the target's local body must run (e.g. medicalsuture's
	/// per-second bleed reduction). Empty for immediate-only uses. The host does
	/// not simulate these; the target re-reports through the normal snapshot path.
	/// </summary>
	[ProtoMember(9)]
	public List<TimedLimbEffectMsg> TimedEffects { get; set; } = [];

	/// <summary>
	/// Timed body-level liquid effects the target's local body must run (e.g.
	/// stimulant, procoagulant, epinephrine and oxyline injections). The host
	/// does not simulate these timed/random branches; the target re-reports
	/// through the normal snapshot path.
	/// </summary>
	[ProtoMember(10)]
	public List<TimedBodyEffectMsg> TimedBodyEffects { get; set; } = [];

	/// <summary>
	/// The drain the host committed for the migrated TOPICAL family: the operator
	/// measured one native <c>ApplyToLimb</c> call, the host capped it at what the
	/// authoritative item carried and split it the way
	/// <c>WaterContainerItem.CalculateDrain</c> does. Non-empty means the target's
	/// own client applies it through the native path, and that
	/// <see cref="Health"/>/<see cref="Limbs"/> carry nothing — the host computed no
	/// body state for this family. Empty for every other family.
	/// </summary>
	[ProtoMember(11)]
	public List<LiquidStackMsg> AppliedDose { get; set; } = [];

	/// <summary>
	/// The limb the operator's gesture selected, or -1 for the ordinary
	/// most-injured-limb rule. It accompanies <see cref="AppliedDose"/>: the
	/// affected side resolves it against its own body, exactly as the injection
	/// chain's <c>NativeInjectionApply</c> does.
	/// </summary>
	public int LimbIndex
	{
		get => _limbSelection <= 0 ? -1 : _limbSelection - 1;
		set => _limbSelection = value >= 0 ? value + 1 : 0;
	}

	private int _limbSelection;

	/// <summary>
	/// Wire representation of <see cref="LimbIndex"/>. Zero means "no explicit
	/// selection"; a positive value is stored as <c>limbIndex + 1</c> so limb 0
	/// is not omitted by protobuf's default-zero rule.
	/// </summary>
	[ProtoMember(12)]
	public int LimbSelection
	{
		get => _limbSelection;
		set => _limbSelection = value;
	}
}
