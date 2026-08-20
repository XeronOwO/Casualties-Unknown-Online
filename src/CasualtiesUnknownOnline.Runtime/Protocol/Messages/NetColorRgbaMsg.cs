using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>Wire form of <see cref="NetColorRgba"/>. Only present when the
/// carrying message's has-value flag is set (the zero-omission convention —
/// a color is carried behind an explicit bool, never inferred from (0,0,0,0)).</summary>
[ProtoContract]
public sealed class NetColorRgbaMsg
{
	public NetColorRgbaMsg()
	{
	}

	public NetColorRgbaMsg(float r, float g, float b, float a)
	{
		R = r;
		G = g;
		B = b;
		A = a;
	}

	[ProtoMember(1)]
	public float R { get; set; }

	[ProtoMember(2)]
	public float G { get; set; }

	[ProtoMember(3)]
	public float B { get; set; }

	[ProtoMember(4)]
	public float A { get; set; }

	/// <summary>Wire → domain; the reverse lives in <see cref="NetColorRgba.ToNetColorRgbaMsg"/>.</summary>
	public NetColorRgba ToNetColorRgba() => new(R, G, B, A);
}
