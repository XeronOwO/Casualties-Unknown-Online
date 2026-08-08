using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>One liquid stack inside a WaterContainerItem (liquidId + amount).</summary>
[ProtoContract]
public sealed class LiquidStackMsg
{
	[ProtoMember(1)]
	public string LiquidId { get; set; } = "";

	[ProtoMember(2)]
	public float Amount { get; set; }
}
