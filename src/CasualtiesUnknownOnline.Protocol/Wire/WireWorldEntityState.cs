using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>
/// Wire form of the authoritative world-entity fact table: one-shot trap
/// consumptions, building-entity health, and opened lockable entities.
/// </summary>
[ProtoContract]
public sealed class WireWorldEntityState
{
	[ProtoMember(1)]
	public List<WireTrapConsumption> Consumptions { get; set; } = [];

	[ProtoMember(2)]
	public List<WireBuildingEntityHealth> BuildingHealth { get; set; } = [];

	[ProtoMember(3)]
	public List<WireOpenedEntity> OpenedEntities { get; set; } = [];
}
