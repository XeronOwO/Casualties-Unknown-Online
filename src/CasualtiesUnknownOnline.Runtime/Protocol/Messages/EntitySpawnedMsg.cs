using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// A world entity was created at RUNTIME (outside world generation — the spawn
/// command, a scripted create): bidirectional, BlockPlaced semantics — the
/// creating side keeps its local copy, reports; the host creates its own copy
/// at the same position and relays (the source excluded); every receiving side
/// creates the same entity. World generation is deterministic on both sides,
/// so the position-keyed identity holds for RUNTIME creations too once both
/// sides hold the entity. Items do NOT ride this channel — the item domain
/// (ItemInstanceId + ItemSpawn) already syncs runtime item creation.
/// </summary>
[ProtoContract]
public sealed class EntitySpawnedMsg
{
	/// <summary>The entity's prefab id (BuildingEntity.id / Utils.Create's first argument, e.g. "landmine").</summary>
	[ProtoMember(1)]
	public string Id { get; set; } = string.Empty;

	/// <summary>The entity's world position.</summary>
	[ProtoMember(2)]
	public NetVector2Msg Position { get; set; } = new();

	/// <summary>The entity's z rotation (euler).</summary>
	[ProtoMember(3)]
	public float Rotation { get; set; }
}
