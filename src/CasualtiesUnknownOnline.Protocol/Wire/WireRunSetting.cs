using ProtoBuf;

namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>
/// One typed run-setting value on the wire. Kind matches
/// <c>RunSettingKind</c>; only the carrier matching Kind is meaningful.
/// </summary>
[ProtoContract]
public sealed class WireRunSetting
{
	[ProtoMember(1)]
	public string Key { get; set; } = "";

	[ProtoMember(2)]
	public int Kind { get; set; }

	[ProtoMember(3)]
	public int IntValue { get; set; }

	[ProtoMember(4)]
	public float FloatValue { get; set; }

	[ProtoMember(5)]
	public bool BoolValue { get; set; }

	[ProtoMember(6)]
	public string StringValue { get; set; } = "";
}
