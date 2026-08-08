using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>Wire form of <see cref="NetworkEntityId"/> (session epoch + host allocation counter + generation).</summary>
[ProtoContract]
public sealed class NetworkEntityIdMsg
{
	public NetworkEntityIdMsg()
	{
	}

	public NetworkEntityIdMsg(ulong epoch, uint counter, byte generation)
	{
		Epoch = epoch;
		Counter = counter;
		Generation = generation;
	}

	[ProtoMember(1)]
	public ulong Epoch { get; set; }

	[ProtoMember(2)]
	public uint Counter { get; set; }

	[ProtoMember(3)]
	public uint Generation { get; set; }

	/// <summary>Wire → domain; the reverse lives in <see cref="NetworkEntityIdMsgExtensions"/>.</summary>
	public NetworkEntityId ToNetworkEntityId() => new(Epoch, Counter, (byte)Generation);
}
