using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// A transient fluid-presentation event the host sends to a guest whose
/// viewport contains the event: a water push (the game's <c>WaterPusher</c>
/// object that pushes/slips the local body) or a waterflow sound
/// (<c>waterflow1..3</c>). The host is the fluid authority (#129): it simulates
/// the world grid alone, and the guest never simulates — so the guest would
/// otherwise miss the water-push physics and the waterflow ambience that the
/// host's <c>FluidManager.SimulationStep</c> produces. The host replays these
/// transient effects as dedicated reliable messages (one event = one message),
/// the same shape as the character-action sounds. The authoritative fluid grid
/// itself keeps riding <see cref="FluidRegionMsg"/>.
/// </summary>
[ProtoContract]
public sealed class FluidPresentationMsg
{
	/// <summary>A water-push <c>WaterPusher</c> object at the cell (the local
	/// body gets pushed/slipped while it overlaps the 0.75 s collider).</summary>
	public const byte KindWaterPush = 1;

	/// <summary>The <c>waterflow1..3</c> sound at the cell.</summary>
	public const byte KindWaterflowSound = 2;

	[ProtoMember(1)]
	public byte Kind { get; set; }

	/// <summary>The event's grid cell (block coordinates).</summary>
	[ProtoMember(2)]
	public int X { get; set; }

	[ProtoMember(3)]
	public int Y { get; set; }

	/// <summary>WaterPush only: the flow direction (Vector2.down/right/left as
	/// the host's simulation moved the cell).</summary>
	[ProtoMember(4)]
	public float DirX { get; set; }

	[ProtoMember(5)]
	public float DirY { get; set; }

	/// <summary>WaterflowSound only: the chosen clip suffix (1..3) — the host
	/// already consumed the public <c>Random.Range(1,4)</c>, so the receiver
	/// plays the exact clip without consuming random again.</summary>
	[ProtoMember(6)]
	public byte SoundIndex { get; set; }
}
