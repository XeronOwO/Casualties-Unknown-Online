using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>
/// Wire form of the kernel-owned save-shaped item payload.
/// </summary>
[ProtoContract]
public sealed class WireItemData
{
	[ProtoMember(1)]
	public float Condition { get; set; }

	[ProtoMember(2)]
	public bool Favourited { get; set; }

	[ProtoMember(3)]
	public int SlotIndex { get; set; }

	[ProtoMember(4)]
	public List<WireLiquidStack> Liquids { get; set; } = [];

	[ProtoMember(5)]
	public List<WireComponentState> Components { get; set; } = [];
}
