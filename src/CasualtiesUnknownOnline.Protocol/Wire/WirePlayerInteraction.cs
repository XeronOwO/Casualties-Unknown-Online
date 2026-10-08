using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>
/// Wire payload for the three typed player-interaction result events
/// (inventory transfer, heal, item use). One composite keeps the event DTO from
/// adding many parallel scalar members to <see cref="WireEvent"/>; the
/// <see cref="WireEvent.Kind"/> discriminates which fields are meaningful.
/// </summary>
[ProtoContract]
public sealed class WirePlayerInteraction
{
	[ProtoMember(1)]
	public ulong FromSteamId { get; set; }

	[ProtoMember(2)]
	public ulong ToSteamId { get; set; }

	[ProtoMember(3)]
	public WireItemIdentity? ItemIdentity { get; set; }

	[ProtoMember(4)]
	public WireItemData? ItemData { get; set; }

	[ProtoMember(5)]
	public ulong ItemInstanceId { get; set; }

	[ProtoMember(6)]
	public bool ItemDestroyed { get; set; }

	[ProtoMember(7)]
	public float ItemConditionAfter { get; set; }

	[ProtoMember(8)]
	public int HealedLimbIndex { get; set; }

	[ProtoMember(9)]
	public WirePlayerInteractionHealth? Health { get; set; }

	[ProtoMember(10)]
	public List<WirePlayerInteractionLimb> Limbs { get; set; } = [];

	[ProtoMember(11)]
	public List<WirePlayerInteractionTimedLimbEffect> TimedEffects { get; set; } = [];

	[ProtoMember(13)]
	public WireItemIdentity? ItemAfterIdentity { get; set; }

	[ProtoMember(14)]
	public WireItemData? ItemAfterData { get; set; }

	[ProtoMember(15)]
	public WireItemIdentity? WornItemIdentity { get; set; }

	[ProtoMember(16)]
	public WireItemData? WornItemData { get; set; }

	[ProtoMember(17)]
	public List<WirePlayerInteractionItem> ItemContents { get; set; } = [];

	[ProtoMember(18)]
	public List<WirePlayerInteractionItem> ItemAfterContents { get; set; } = [];

	[ProtoMember(19)]
	public List<WirePlayerInteractionItem> WornItemContents { get; set; } = [];

	/// <summary>For a same-owner container move, the destination container's instance id.</summary>
	[ProtoMember(20)]
	public ulong TargetParentItemId { get; set; }

	/// <summary>
	/// The drain the host committed for a migrated topical use, and the limb the
	/// operator's gesture selected. Both ride the item-use event because the
	/// affected side applies the effect; both are empty/-1 for every other
	/// family. The limb rides as <c>limbIndex + 1</c> so limb 0 survives
	/// protobuf's default-zero rule — the same encoding the use request uses.
	/// </summary>
	[ProtoMember(21)]
	public List<WireLiquidStack> AppliedDose { get; set; } = [];

	private int _limbSelection;

	[ProtoMember(22)]
	public int LimbSelection
	{
		get => _limbSelection;
		set => _limbSelection = value;
	}

	/// <summary>The selected limb, or -1 for the ordinary most-injured-limb rule.</summary>
	public int LimbIndex
	{
		get => _limbSelection <= 0 ? -1 : _limbSelection - 1;
		set => _limbSelection = value >= 0 ? value + 1 : 0;
	}

	/// <summary>
	/// The drain the host committed for a migrated drink: the affected side runs
	/// each liquid's own <c>onDrink</c> body over it, which is a different native
	/// call from <see cref="AppliedDose"/>'s limb application, so the two ride
	/// their own members rather than one list plus a discriminator. Empty for
	/// every other family.
	/// </summary>
	[ProtoMember(23)]
	public List<WireLiquidStack> DrinkDose { get; set; } = [];

	/// <summary>
	/// True for the solid-food family's request half: the target's own client runs
	/// the item's own use action, and the item's post-eat state follows in the
	/// second result the host publishes from the eater's outcome report. Nothing
	/// else in the payload is meaningful for that half. False for every other
	/// family and for that second result.
	/// </summary>
	[ProtoMember(24)]
	public bool TargetEatsTheItem { get; set; }
}
