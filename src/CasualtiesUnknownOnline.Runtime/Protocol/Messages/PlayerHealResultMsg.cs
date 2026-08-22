using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Host → participant(s) authoritative result of a cross-player heal. One
/// operation = one message: the healer learns whether its item was consumed or
/// destroyed, and the target receives the host-computed post-heal health and
/// full limb state so its local body can apply the exact same medical effect
/// inside a RemoteApply scope. The target re-reports its character snapshot
/// immediately, so the host save and every peer clone converge without waiting
/// for the next 1 Hz tick.
/// </summary>
[ProtoContract]
public sealed class PlayerHealResultMsg
{
	/// <summary>The player whose medical item was consumed.</summary>
	[ProtoMember(1)]
	public ulong HealerSteamId { get; set; }

	/// <summary>The player who received the healing.</summary>
	[ProtoMember(2)]
	public ulong TargetSteamId { get; set; }

	/// <summary>The consumed item's stable instance id (0 = auto-select/unknown).</summary>
	[ProtoMember(3)]
	public ulong ItemInstanceId { get; set; }

	/// <summary>True when the item's condition reached zero and the item is destroyed.</summary>
	[ProtoMember(4)]
	public bool ItemDestroyed { get; set; }

	/// <summary>The item's condition after the heal (0 when destroyed).</summary>
	[ProtoMember(5)]
	public float ItemConditionAfter { get; set; }

	/// <summary>The limb index the heal was applied to (-1 = no limb data).</summary>
	[ProtoMember(6)]
	public int HealedLimbIndex { get; set; } = -1;

	/// <summary>The target's post-heal body health state.</summary>
	[ProtoMember(7)]
	public CharacterHealthMsg? Health { get; set; }

	/// <summary>The target's post-heal full limb state.</summary>
	[ProtoMember(8)]
	public List<CharacterLimbMsg> Limbs { get; set; } = [];
}
