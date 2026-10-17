using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Host → requester: the intent the requester sent was refused, and this is why. One refusal per gesture — never one
/// per frame, and never one for a refusal the requester's own client already made and printed.
/// <para>
/// The reason travels as the SAME sentence the host logged, rather than a code the two sides would have to keep in
/// step: the family's rule is that a refusal is answered so both ends agree, and two wordings for one refusal is how
/// they stop agreeing.
/// </para>
/// </summary>
[ProtoContract]
public sealed class PlayerInventoryIntentRefusedMsg
{
	/// <summary>The refused intent kind, as <see cref="RemoteInventoryIntentKind"/>.</summary>
	[ProtoMember(1)]
	public int Kind { get; set; }

	/// <summary>The item the intent named; 0 when the intent named none.</summary>
	[ProtoMember(2)]
	public ulong ItemInstanceId { get; set; }

	/// <summary>Why it was refused, in the words the host logged.</summary>
	[ProtoMember(3)]
	public string Reason { get; set; } = "";
}
