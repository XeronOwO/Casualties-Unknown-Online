using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// A player-lit dynamite item detonated (CustomItemBehaviour.DynamiteExplode,
/// Item.cs:6671-6682 + CustomItemBehaviour.cs:563-572). The trigger side's
/// game already ran the native explosion; this dedicated event lets the other
/// sides apply the same body/visual consequences (the world-terrain, building
/// and item-damage consequences ride the existing block/building/world-item
/// channels from the trigger side's own explosion, so this event only needs
/// the detonation position). Star semantics: guest → host report, host applies
/// to its own world and relays (source excluded), guests replay the explosion.
/// </summary>
[ProtoContract]
public sealed class DynamiteExplosionMsg
{
	/// <summary>The destroyed dynamite item's instance id — the one-shot
	/// identity for duplicate suppression (an item can detonate at most once).</summary>
	[ProtoMember(1)]
	public ulong ItemInstanceId { get; set; }

	/// <summary>The world position of the detonated dynamite (the item's transform position at detonation).</summary>
	[ProtoMember(2)]
	public NetVector2Msg Position { get; set; } = new();
}
