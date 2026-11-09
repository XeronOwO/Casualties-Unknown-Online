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

	/// <summary>The player's own label; null means nobody set one (the wire writes nothing).</summary>
	[ProtoMember(6)]
	public string? Label { get; set; }

	/// <summary>The player's colour swatch index (the game's own <c>colorSwatch</c>); 0 is the default swatch.</summary>
	[ProtoMember(7)]
	public int ColorSwatch { get; set; }
}
