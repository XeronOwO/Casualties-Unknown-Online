using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// One runtime-creation identity on the wire (sync-coverage audit E3): the same
/// fields the recovery tables key by — prefab id, floored creation cell and the
/// creation-instance token. Carried by <see cref="RuntimeEntitySnapshotMsg"/>
/// for ACKNOWLEDGEMENT ONLY: the host's accepted-creation table excludes
/// animals (the enemy domain owns their late-join copy), so the snapshot's
/// entry list can never acknowledge a guest's animal report — this key list
/// does, without ever materializing anything.
/// </summary>
[ProtoContract]
public sealed class RuntimeEntityKeyMsg
{
	/// <summary>The created entity's prefab id (BuildingEntity.id).</summary>
	[ProtoMember(1)]
	public string Id { get; set; } = string.Empty;

	/// <summary>The floored X of the CREATION position (never the drifted current position).</summary>
	[ProtoMember(2)]
	public int CellX { get; set; }

	/// <summary>The floored Y of the CREATION position.</summary>
	[ProtoMember(3)]
	public int CellY { get; set; }

	/// <summary>The creation-instance token's creator half (see <see cref="EntitySpawnedMsg.CreatorSteamId"/>).</summary>
	[ProtoMember(4)]
	public ulong CreatorSteamId { get; set; }

	/// <summary>The creation-instance token's sequence half (see <see cref="EntitySpawnedMsg.CreationSequence"/>).</summary>
	[ProtoMember(5)]
	public uint CreationSequence { get; set; }
}
