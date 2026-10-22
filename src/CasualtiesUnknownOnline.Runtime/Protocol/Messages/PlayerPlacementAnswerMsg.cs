using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Receiving client → host: where THIS client's own rules would put the queried item. An accepted answer names
/// exactly one destination — the body slot it will use (possibly a substitute for the giver's request) or the
/// container instance it will load the item into — and the host records THAT, never the giver's request. A
/// refusal carries the reason the receiving rules gave, and the host refuses the giver with the same sentence:
/// nothing moved, and the item is still in the giver's inventory.
/// </summary>
[ProtoContract]
public sealed class PlayerPlacementAnswerMsg
{
	/// <summary>The query this answers, as the host issued it.</summary>
	[ProtoMember(1)]
	public ulong QueryId { get; set; }

	/// <summary>True when this client will take the item; false when its own rules refuse it.</summary>
	[ProtoMember(2)]
	public bool Accepted { get; set; }

	/// <summary>The body slot this client will place the item in, or -1 for a container destination.</summary>
	[ProtoMember(3)]
	public int Slot { get; set; } = -1;

	/// <summary>The container instance this client will load the item into, or 0 for a body-slot destination.</summary>
	[ProtoMember(4)]
	public ulong ContainerInstanceId { get; set; }

	/// <summary>The receiving rules' own reason when <see cref="Accepted"/> is false; empty otherwise.</summary>
	[ProtoMember(5)]
	public string Reason { get; set; } = "";
}
