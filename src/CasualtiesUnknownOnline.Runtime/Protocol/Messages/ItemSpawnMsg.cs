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
}
