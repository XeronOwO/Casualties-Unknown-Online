using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// A runtime-generated item entered the world (block-destroy drop, creature
/// loot, use-spawned item): guest → host as a report (the host registers it in
/// the authoritative world-item table and relays), host → guest as a broadcast
/// relay (the source excluded — it already applied locally). Carries the full
/// item state (the character-save shape: condition + components + container
/// contents) so receivers can materialize the object exactly. Generation-time
/// items never take this path — world-gen determinism covers them.
/// </summary>
[ProtoContract]
public sealed class ItemSpawnMsg
{
	[ProtoMember(1)]
	public ulong ItemId { get; set; } // instance id: (spawner SteamId, local counter)

	[ProtoMember(2)]
	public CharacterItemMsg Item { get; set; } = new();

	[ProtoMember(3)]
	public NetVector2Msg Position { get; set; } = new();

	[ProtoMember(4)]
	public NetVector2Msg Velocity { get; set; } = new();

	[ProtoMember(5)]
	public float Rotation { get; set; } // z euler angle — spawns carry random rotations

	[ProtoMember(6)]
	public bool FreshItemDrop { get; set; } // the glowing floating pickup effect (FreshItemDrop.cs) — carries over to the remote spawn

	[ProtoMember(7)]
	public float AngularVelocity { get; set; } // the item's spin at the spawn moment (a rolling drop's initial condition)
}
