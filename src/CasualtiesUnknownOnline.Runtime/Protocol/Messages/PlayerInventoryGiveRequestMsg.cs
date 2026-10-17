using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Guest → host: the member typed <c>/give</c>. The host is the creation authority — it owns
/// the definition table, the prefab capture and the instance id space — so a member's verb is
/// a REQUEST, never a local creation.
///
/// <para>
/// <see cref="State"/> is the item's state as the canonical SNBT text the member's parser
/// produced (empty = the definition's own captured state). The host re-parses it and binds it
/// against ITS capture of the definition, so a member cannot hand over a state the definition
/// does not carry. That is what makes the permission on this message the host's own ruling
/// rather than a courtesy copy of the member's.
/// </para>
///
/// <para>
/// Text rather than a pre-built item fact on purpose: ONE data language exists in the tree,
/// both ends run the same parser and the same adapter bind, and the host's refusal is the
/// sentence that bind already prints. The text's length is bounded by the verb's own state
/// bound and its parse depth is the parser's own.
/// </para>
/// </summary>
[ProtoContract]
public sealed class PlayerInventoryGiveRequestMsg
{
	/// <summary>The ONE body that receives the item (a selector that named several was refused at the member's console).</summary>
	[ProtoMember(1)]
	public ulong TargetSteamId { get; set; }

	/// <summary>The item definition id the operator named (a bare item id, e.g. <c>bandage</c>).</summary>
	[ProtoMember(2)]
	public string DefinitionId { get; set; } = string.Empty;

	/// <summary>The destination body slot, or -1 for the first free slot.</summary>
	[ProtoMember(3)]
	public int SlotIndex { get; set; } = -1;

	/// <summary>The state attached to the item id, in the canonical SNBT form; empty when the definition's own capture is the state.</summary>
	[ProtoMember(4)]
	public string State { get; set; } = string.Empty;
}
