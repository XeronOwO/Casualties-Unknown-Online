using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>One run-setting value. Protobuf has no polymorphic containers, so the
/// value rides in the member matching <see cref="Kind"/> (same kind dispatch as
/// the old WriteRunSettings).</summary>
[ProtoContract]
public sealed class SettingEntryMsg
{
	[ProtoMember(1)]
	public string Key { get; set; } = "";

	/// <summary>1 = int, 2 = float, 3 = bool, 4 = string.</summary>
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
