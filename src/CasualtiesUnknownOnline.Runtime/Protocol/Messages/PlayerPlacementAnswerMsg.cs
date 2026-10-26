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

	/// <summary>
	/// The body slot this client will place the item in, ENCODED as <c>slot + 1</c> — 0 means "no body slot" (a container destination, or a refusal).
	///
	/// <para>
	/// protobuf-net omits a 0-valued int, so a plain slot field cannot carry slot 0 at all (the trap <c>CharacterDataMsg.HandSlot</c> documents).
	/// A give answered for the receiver's first slot therefore arrived as the default and the host refused it as "accepted without naming a slot"
	/// (measured 2026-10-26). Write <c>slot + 1</c>, read <c>Slot - 1</c>.
	/// </para>
	/// </summary>
	[ProtoMember(3)]
	public int Slot { get; set; }

	/// <summary>The container instance this client will load the item into, or 0 for a body-slot destination.</summary>
	[ProtoMember(4)]
	public ulong ContainerInstanceId { get; set; }

	/// <summary>The receiving rules' own reason when <see cref="Accepted"/> is false; empty otherwise.</summary>
	[ProtoMember(5)]
	public string Reason { get; set; } = "";
}
