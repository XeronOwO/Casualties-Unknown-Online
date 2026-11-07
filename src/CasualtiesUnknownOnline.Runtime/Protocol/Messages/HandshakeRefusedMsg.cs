using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Host → guest: this handshake was refused, and here is which identity
/// dimension failed with the two values. The member renders the sentence from
/// its own catalogue (`handshake.refused.&lt;dimension&gt;`) because the host
/// cannot know the member's language; <see cref="Detail"/> is the mod id for a
/// mod answer and empty otherwise.
/// </summary>
[ProtoContract]
public sealed class HandshakeRefusedMsg
{
	/// <summary>The failing dimension as <see cref="HandshakeDimension"/>'s number.</summary>
	[ProtoMember(1)]
	public byte Dimension { get; set; }

	/// <summary>What failed inside the dimension (the mod id), or empty.</summary>
	[ProtoMember(2)]
	public string Detail { get; set; } = "";

	/// <summary>The host's value for the dimension (a build string, a schema number, a mod version).</summary>
	[ProtoMember(3)]
	public string HostValue { get; set; } = "";

	/// <summary>The member's value, as the host read it from the handshake.</summary>
	[ProtoMember(4)]
	public string GuestValue { get; set; } = "";
}
