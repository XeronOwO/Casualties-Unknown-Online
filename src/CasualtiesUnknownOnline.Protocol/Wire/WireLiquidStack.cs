using ProtoBuf;

namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>
/// One liquid stack inside a water-container item.
/// </summary>
[ProtoContract]
public sealed class WireLiquidStack
{
	[ProtoMember(1)]
	public string LiquidId { get; set; } = "";

	[ProtoMember(2)]
	public float Amount { get; set; }
}
