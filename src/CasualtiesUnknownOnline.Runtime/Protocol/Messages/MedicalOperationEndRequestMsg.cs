using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Operator → host: the native minigame ended. <c>TotalMl</c> is the exact
/// cumulative amount the operator physically delivered; the host reconciles it
/// against the already-committed deltas, commits any remaining unreported ml
/// once, and emits exactly one <see cref="MedicalOperationEndCommittedMsg"/>.
/// </summary>
[ProtoContract]
public sealed class MedicalOperationEndRequestMsg
{
	[ProtoMember(1)]
	public ulong OperationId { get; set; }

	[ProtoMember(2)]
	public float TotalMl { get; set; }
}
