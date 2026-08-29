using ProtoBuf;

namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>
/// Wire form of one authoritative enemy/entity fact.
/// </summary>
[ProtoContract]
public sealed class WireEnemyState
{
	[ProtoMember(1)]
	public WireEntityId EntityId { get; set; } = new();

	[ProtoMember(2)]
	public string PrefabId { get; set; } = "";

	[ProtoMember(3)]
	public float Health { get; set; }

	[ProtoMember(4)]
	public bool RuntimeSpawned { get; set; }

	[ProtoMember(5)]
	public bool Stunned { get; set; }
}
