using CasualtiesUnknownOnline.Protocol.Wire;

namespace CasualtiesUnknownOnline.Application.Kernel;

/// <summary>
/// The guest's pending-command bookkeeping: remember an unacknowledged command
/// so a swallowed one can be re-reported, and close it when the host answers —
/// by committing it or by refusing it.
/// </summary>
public interface IKernelPendingCommands
{
	/// <summary>Remember a command that may need a re-report.</summary>
	void Track(WireCommand command, ulong operationId, ProtocolFrame frame, WirePayloadType payloadType);

	/// <summary>The host committed this operation — the command is answered.</summary>
	void ClearCommitted(ulong operationId);

	/// <summary>The host refused this item's command — the pending entry is answered.</summary>
	void ClearRejected(ulong itemId);
}
