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
}
