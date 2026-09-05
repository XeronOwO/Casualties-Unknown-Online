using ProtoBuf;

namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>
/// Wire identity of one item: runtime instance id plus game definition id.
/// </summary>
[ProtoContract]
public sealed class WireItemIdentity
{
	[ProtoMember(1)]
	public ulong InstanceId { get; set; }

	[ProtoMember(2)]
	public string DefinitionId { get; set; } = "";

	/// <summary>
	/// 1-based index into a checkpoint's <see cref="WireCheckpoint.ItemDefinitionTable"/>.
	/// 0 = no table entry; <see cref="DefinitionId"/> is the direct string. Checkpoint
	/// split/assemble uses this to avoid repeating the same game item definition id on
	/// every item in a large correctness snapshot.
	/// </summary>
	[ProtoMember(3)]
	public int DefinitionIndex { get; set; }
}
