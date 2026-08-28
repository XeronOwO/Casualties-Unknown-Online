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
}
