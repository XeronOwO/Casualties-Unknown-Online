using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Host → participant(s) authoritative result of a cross-player consumable use.
/// One operation = one message: the acting player learns whether its item was
/// consumed, destroyed or had its liquid stack drained, and the target receives
/// the effect. Two families have migrated to the affected side's own client — the
/// topical application and the drink (injection rides its own session wire) — and
/// for those the result carries the committed drain instead of any host-computed
/// body state; every other family still receives the host-computed post-use body
/// state and applies it inside a RemoteApply scope. The target re-reports its
/// character snapshot immediately after applying either kind, so the host save and
/// every peer clone converge without waiting for the next 1 Hz tick.
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

	/// <summary>
	/// The drain the host committed for the migrated DRINK family: the operator
	/// measured one native <c>WaterContainerItem.Drink</c> call, the host capped
	/// it at what the authoritative item carried and split it the way
	/// <c>CalculateDrain</c> does. Non-empty means the target's own client runs
	/// each liquid's own <c>onDrink</c> body over it, and that
	/// <see cref="Health"/>/<see cref="Limbs"/> carry nothing — like the topical
	/// family, the host computed no body state here. Empty for every other
	/// family.
	/// <para>
	/// It is its own field rather than a flag beside <see cref="AppliedDose"/>
	/// because the two name different native calls on the affected side
	/// (<c>Drink</c>'s per-stack <c>onDrink</c> against <c>ApplyToLimb</c>'s
	/// per-stack <c>onHealthUse</c>, which needs a limb) — so neither family has
	/// to be inferred from the other's emptiness.
	/// </para>
	/// </summary>
	[ProtoMember(13)]
	public List<LiquidStackMsg> DrinkDose { get; set; } = [];

	/// <summary>
	/// True for the SOLID-FOOD family's request half: the TARGET's own client must
	/// run the game's own use action (<c>Body.UseItem</c> → <c>Stats.useAction</c>)
	/// against its own body and its own object of this item
	/// (<see cref="ItemInstanceId"/>), because that delegate writes the eating body
	/// and the item it is handed. Everything else in this message is empty for the
	/// request half — the host computed no body state and changed no item state —
	/// and the item's own post-eat state follows in the SECOND result the host
	/// publishes from the eater's outcome report
	/// (<see cref="PlayerItemActionOutcomeMsg"/>), whose <see cref="UserSteamId"/> is
	/// the item's owner.
	/// </summary>
	[ProtoMember(14)]
	public bool TargetEatsTheItem { get; set; }

	/// <summary>
	/// True for the LIMB-TOOL family's request half: the TREATED player's own client
	/// must run the item's own limb action (<c>ItemInfo.useLimbAction</c>) against its
	/// own limb and its own object of this item
	/// (<see cref="ItemInstanceId"/>), because that delegate writes the limb, the limb
	/// component it may turn into and the item it is handed. <see cref="LimbIndex"/>
	/// names the limb it must treat. Like the eat above, everything else is empty for
	/// this half and the item's post-use state follows in the second result the host
	/// publishes from that side's outcome report.
	/// </summary>
	[ProtoMember(15)]
	public bool TargetRunsLimbAction { get; set; }
}
