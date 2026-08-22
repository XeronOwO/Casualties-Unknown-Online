using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// The host-authoritative tutorial-claw presentation stream (host → guest).
/// The TutorialHandler's claw is a world-space rig whose continuous motion
/// (handPos / handPosCurrent) is otherwise per-side; this 20 Hz absolute
/// snapshot lets a guest that is not running its own course render the same
/// claw flow as the host. Position fields are absolute world coordinates.
/// Material is carried directly because the game derives it from a Transform
/// reference (grabInfo.Item1), which a presentation-only receiver does not
/// possess.
/// </summary>
[ProtoContract]
public sealed class TutorialClawStateMsg
{
	/// <summary>No grabbed object — the claw is open.</summary>
	public const byte GrabNone = 0;

	/// <summary>The claw is grabbing a Body.</summary>
	public const byte GrabBody = 1;

	/// <summary>The claw is grabbing an Item.</summary>
	public const byte GrabItem = 2;

	/// <summary>The claw is grabbing a BuildingEntity.</summary>
	public const byte GrabBuilding = 3;

	/// <summary>Open material (grabInfo empty).</summary>
	public const byte MaterialOpen = 0;

	/// <summary>Closed material (grabInfo non-empty).</summary>
	public const byte MaterialClosed = 1;

	/// <summary>Place-material (block place queue non-empty).</summary>
	public const byte MaterialPlace = 2;

	/// <summary>Knife material (armKnifeSpriteOverride).</summary>
	public const byte MaterialKnife = 3;

	/// <summary>The unreliable-stream sequence number (host-assigned; stale/duplicate frames are dropped).</summary>
	[ProtoMember(1)]
	public uint Seq { get; set; }

	/// <summary>The claw's target position (host TutorialHandler.handPos).</summary>
	[ProtoMember(2)]
	public float HandPosX { get; set; }

	[ProtoMember(3)]
	public float HandPosY { get; set; }

	/// <summary>The claw's current rendered position (host TutorialHandler.handPosCurrent).</summary>
	[ProtoMember(4)]
	public float HandPosCurrentX { get; set; }

	[ProtoMember(5)]
	public float HandPosCurrentY { get; set; }

	/// <summary>What the host claw is holding (TutorialHandler.grabInfo.Item2 mapped 1:1 to <c>GrabBody</c>/<c>GrabItem</c>/<c>GrabBuilding</c>).</summary>
	[ProtoMember(6)]
	public byte GrabKind { get; set; }

	/// <summary>The claw-arm material to show on the receiving side.</summary>
	[ProtoMember(7)]
	public byte Material { get; set; }

	/// <summary>Host TutorialHandler.armKnifeSpriteOverride (also reflected in Material, kept as a flag because the game toggles it independently).</summary>
	[ProtoMember(8)]
	public bool ArmKnifeSpriteOverride { get; set; }
}
