using CasualtiesUnknownOnline.Runtime.GameAdapter;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Guest → host: the member typed <c>/summon</c>. The host is the creation authority — its content set decides
/// whether the prefab can be materialized at all, and only the host's own creation is the record the world-entry
/// group carries to a later joiner — so a member's verb is a REQUEST, never a local creation.
///
/// <para>
/// <see cref="Kind"/> is the operator's own type word (<c>item</c> or a bare prefab id). It travels because the
/// host must enforce it: a request that asks for an item entity but names a prefab, or the reverse, is refused by
/// name on the host rather than guessed into the wrong family.
/// </para>
///
/// <para>
/// The POSITION is already resolved: the member's client turned the position keyword (<c>cursor</c>,
/// <c>player</c>, <c>random</c>) into a world point on its own scene, because the pointer it named is its
/// pointer and the body it named is its body. The host creates at that point. The claim is a creation cause —
/// the same class the item domain's own <c>ItemSpawn</c> report is — and the member-report census says so.
/// </para>
///
/// <para>
/// <see cref="State"/> is the item entity's state as the canonical SNBT text the member's parser produced (empty
/// = the definition's own captured state), exactly the give request's field: the host re-parses it and binds it
/// against ITS capture of the item, so a member cannot hand over a state the definition does not carry. An entity
/// prefab takes no state, and the host's creation seam refuses one by name.
/// </para>
///
/// <para>
/// <see cref="Generation"/> is the world/layer this position belongs to — the same kernel run baseline every
/// stamped world report carries. Positions are layer-relative, so a request that outlived a layer boundary must
/// be refused instead of summoning at the old layer's coordinates in the new one.
/// </para>
/// </summary>
[ProtoContract]
public sealed class WorldSummonRequestMsg
{
	/// <summary>The prefab id the operator named (the game's own item id or prefab id, e.g. <c>bandage</c>, <c>beartrap</c>).</summary>
	[ProtoMember(1)]
	public string PrefabId { get; set; } = string.Empty;

	/// <summary>The state attached to an ITEM entity, in the canonical SNBT form; empty when the definition's own capture is the state (and always empty for an entity prefab).</summary>
	[ProtoMember(2)]
	public string State { get; set; } = string.Empty;

	/// <summary>The world X the member's client resolved its position argument to.</summary>
	[ProtoMember(3)]
	public float X { get; set; }

	/// <summary>The world Y the member's client resolved its position argument to.</summary>
	[ProtoMember(4)]
	public float Y { get; set; }

	/// <summary>The world/layer generation the position belongs to (the kernel run baseline at the member's send point; null = no committed run).</summary>
	[ProtoMember(5)]
	public WorldGenerationMsg? Generation { get; set; }

	/// <summary>Which family the operator asked for — <c>item</c> or a world-entity prefab; the host enforces it against its own tables. Zero is the entity form, so an old-shaped field reads as the safe family.</summary>
	[ProtoMember(6)]
	public WorldSummonKind Kind { get; set; }
}
