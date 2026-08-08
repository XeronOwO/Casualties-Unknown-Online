using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>Wire form of <see cref="NetVector2"/>. Position fields are always
/// present on the wire (the old binary layout wrote (0,0) for missing values).</summary>
[ProtoContract]
public sealed class NetVector2Msg
{
	public NetVector2Msg()
	{
	}

	public NetVector2Msg(float x, float y)
	{
		X = x;
		Y = y;
	}

	[ProtoMember(1)]
	public float X { get; set; }

	[ProtoMember(2)]
	public float Y { get; set; }

	/// <summary>Wire → domain; the reverse lives in <see cref="NetVector2MsgExtensions"/>.</summary>
	public NetVector2 ToNetVector2() => new(X, Y);
}
