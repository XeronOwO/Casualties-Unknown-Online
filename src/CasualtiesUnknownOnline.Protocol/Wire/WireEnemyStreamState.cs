using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>
/// Wire form of one enemy entity's high-frequency stream fields. The stream
/// carries convergent presentation state; identity/health lifecycle facts
/// travel through the kernel enemy table and dedicated domain events.
/// </summary>
[ProtoContract]
public sealed class WireEnemyStreamState
{
	[ProtoMember(1)]
	public WireEntityId EntityId { get; set; } = new();

	[ProtoMember(2)]
	public WireVector2 Position { get; set; } = new();

	[ProtoMember(3)]
	public WireVector2 Velocity { get; set; } = new();

	[ProtoMember(4)]
	public float Rotation { get; set; }

	[ProtoMember(5)]
	public float Health { get; set; }

	[ProtoMember(6)]
	public uint PresentationFlags { get; set; }

	[ProtoMember(7)]
	public List<WireVector2>? SpiderLegTargets { get; set; }

	[ProtoMember(8)]
	public float CrystalWindupAmount { get; set; }

	[ProtoMember(9)]
	public WireVector2? CrystalLineEnd { get; set; }
}
