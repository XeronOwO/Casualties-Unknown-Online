using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Operator → host: cancel an active medical operation. The host keeps all
/// already-committed progression, discards unreported input, releases
/// reservations and emits the sole terminal <see cref="MedicalOperationEndCommittedMsg"/>
/// with <see cref="MedicalOperationTerminalReason.Cancelled"/>.
/// </summary>
[ProtoContract]
public sealed class MedicalOperationCancelMsg
{
	[ProtoMember(1)]
	public ulong OperationId { get; set; }
}
