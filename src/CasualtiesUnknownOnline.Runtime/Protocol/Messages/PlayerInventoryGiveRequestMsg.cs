using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Guest → host: the member typed <c>/give</c>. The host is the creation
/// authority — it owns the definition table, the prefab capture and the instance
/// id space — so a member's verb is a REQUEST, never a local creation.
///
/// <para>
/// The payload is the operator's own words, not a pre-built item fact: the host
/// re-parses and re-applies the properties against ITS capture of the definition,
/// so a member cannot hand over a state the definition does not carry. That is
/// what makes the permission on this message the host's own ruling rather than a
/// courtesy copy of the member's.
/// </para>
///
/// <para>
/// <see cref="Properties"/> are the tokens after the destination and the
/// definition — the same grammar <c>/data set</c> uses (<c>condition</c>,
/// <c>favourited</c>, <c>liquid</c>, <c>component</c>). Tokens rather than a
/// parsed shape keep ONE grammar in the tree: both ends run the same parser, and
/// the host's refusal is the sentence that parser already prints.
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

	/// <summary>The property tokens after the definition and destination, re-parsed and applied by the host.</summary>
	[ProtoMember(4)]
	public List<string> Properties { get; set; } = [];
}
