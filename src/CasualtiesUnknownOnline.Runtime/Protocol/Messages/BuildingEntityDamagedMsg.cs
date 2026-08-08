using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// A player's attack damaged a building entity (plant, crate, creature —
/// Body.cs:1946, the only player-vs-entity damage write): guest → host as a
/// report (the host applies the damage to its own copy — which is what rolls
/// the host-side drops — and relays), host → guest as a broadcast relay (the
/// source excluded — it already applied locally). The entity is identified by
/// its world position: world entities are generated deterministically, so
/// both sides have the same object at the same place.
/// </summary>
[ProtoContract]
public sealed class BuildingEntityDamagedMsg
{
	[ProtoMember(1)]
	public NetVector2Msg Position { get; set; } = new(); // the entity's world position

	[ProtoMember(2)]
	public float Damage { get; set; }
}
