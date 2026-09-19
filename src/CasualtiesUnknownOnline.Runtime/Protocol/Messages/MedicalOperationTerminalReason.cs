namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Why a medical operation session reached its single terminal state. The
/// terminal message is always <c>MedicalOperationEndCommittedMsg</c>; only the
/// reason differs.
/// </summary>
public enum MedicalOperationTerminalReason : int
{
	Completed = 0,
	Cancelled = 1,
	TimedOut = 2,
	Disconnected = 3,

	/// <summary>
	/// Another operator's completion settled the same unit first: this operation
	/// stopped without resolving anything, and the terminal carries the
	/// authoritative state the winner produced.
	/// </summary>
	AlreadyHandled = 4,
}
