using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// ONE heater-cook operation's complete terminal state — the "one operation =
/// one message" rule for the Heater cooker (Heater.cs:41-49): a raw meat item
/// that collides with a cooker is destroyed and replaced by a steak whose
/// condition is 30 % of the raw item's condition. The host's full-physics
/// scene is the only side that can run the native conversion (guest world
/// items are layer-isolated to the Ground layer), so this message is
/// host → guest only: the host applies natively, updates the authoritative
/// world-item table atomically (source removed, cooked steak registered) and
/// broadcasts the complete cooked-item state for the guests to replay.
/// </summary>
[ProtoContract]
public sealed class ItemCookMsg
{
	[ProtoMember(1)]
	public ulong SourceItemId { get; set; } // the raw meat instance id — removed from the world table

	[ProtoMember(2)]
	public ulong CookedItemId { get; set; } // the new steak instance id — registered in the world table

	[ProtoMember(3)]
	public CharacterItemMsg Item { get; set; } = new(); // the full cooked-steak capture (condition + components + contents)

	[ProtoMember(4)]
	public NetVector2Msg Position { get; set; } = new(); // the conversion position (the raw item's transform position, Heater.cs:46)

	[ProtoMember(5)]
	public NetVector2Msg Velocity { get; set; } = new(); // the cooked steak's initial velocity (the prefab default is zero, but capture the live fact)

	[ProtoMember(6)]
	public float Rotation { get; set; } // z euler angle — the raw item's rotation at the conversion moment (Heater.cs:46)

	[ProtoMember(7)]
	public float AngularVelocity { get; set; } // the cooked steak's spin at the conversion moment
}
