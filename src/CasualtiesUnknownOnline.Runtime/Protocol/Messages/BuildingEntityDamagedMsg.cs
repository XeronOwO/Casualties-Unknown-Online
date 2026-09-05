using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// A player's attack damaged a building entity (plant, crate, creature —
/// Body.cs:1946, the only player-vs-entity damage write): guest → host as a
/// report (the host applies the damage to its own copy — which is what rolls
/// the host-side drops — and relays), host → guest as a broadcast relay (the
/// source excluded — it already applied locally). The same channel also
/// carries damage sources that must NOT replay the per-entity hitSound (cactus
/// self-damage from CactusScript.OnCollisionEnter2D — the trigger side plays
/// only the player-local gore sound); <see cref="PlayHitSound"/> keeps that
/// distinction explicit instead of burying it in receiver-side component
/// checks. <see cref="PlayHitFlash"/> marks the one-shot red HitFlash that
/// Body.Attack spawns on the attacker's side (Body.cs:1948-1951); explosion
/// and silent damage sources leave it false so a receiver never invents a
/// presentation the trigger side did not play. The entity is identified by its
/// world position: world entities are generated deterministically, so both
/// sides have the same object at the same place.
/// </summary>
[ProtoContract]
public sealed class BuildingEntityDamagedMsg
{
	[ProtoMember(1)]
	public NetVector2Msg Position { get; set; } = new(); // the entity's world position

	[ProtoMember(2)]
	public float Damage { get; set; }

	[ProtoMember(3)]
	public bool PlayHitSound { get; set; } // false is the protobuf default — attack/explosion sends explicitly set true so the field is serialized

	[ProtoMember(4)]
	public bool PlayHitFlash { get; set; } // false is the protobuf default — Body.Attack sends explicit true so the receiver replays the native red HitFlash
}
