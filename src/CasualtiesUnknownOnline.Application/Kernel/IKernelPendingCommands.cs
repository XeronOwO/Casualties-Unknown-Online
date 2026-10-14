using CasualtiesUnknownOnline.Protocol.Wire;

namespace CasualtiesUnknownOnline.Application.Kernel;

/// <summary>
/// The guest's pending-command bookkeeping: remember an unacknowledged command
/// so a swallowed one can be re-reported, and close it when the host answers —
/// by committing it or by refusing it.
/// </summary>
public interface IKernelPendingCommands
{
	/// <summary>Remember a command that may need a re-report, with the run epoch its frame was stamped for.</summary>
	void Track(WireCommand command, ulong operationId, ProtocolFrame frame, WirePayloadType payloadType, ulong runEpoch);

	/// <summary>The host committed this operation — the command is answered.</summary>
	void ClearCommitted(ulong operationId);

	/// <summary>
	/// The host refused a report about this item, and the verdict names the KIND it answered
	/// (<c>WireCommand.RefusedKind</c>): the entry that leaves the window is the newest one
	/// OF THAT KIND. A verdict that closed the item's newest entry whatever it was would
	/// spend the budget of a report the host never judged.
	/// </summary>
	void ClearRejected(WireCommandKind kind, ulong itemId);
}
