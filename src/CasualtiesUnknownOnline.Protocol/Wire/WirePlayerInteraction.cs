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

	[ProtoMember(12)]
	public List<WirePlayerInteractionTimedBodyEffect> TimedBodyEffects { get; set; } = [];

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
}
