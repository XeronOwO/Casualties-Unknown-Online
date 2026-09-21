using CasualtiesUnknownOnline.GameState;

namespace CasualtiesUnknownOnline.Application.Kernel;

/// <summary>
/// The host's command execution surface: submit a decoded kernel command and
/// read the kernel's verdict. The kernel owns the policy — this port only
/// carries the call and its answer, so the replication surface never second
/// guesses what the kernel decided.
/// </summary>
public interface IKernelCommandExecution
{
	/// <summary>
	/// Execute a command as the given actor. False with a rejection when the
	/// kernel refused it; the committed batch is the kernel's own record of what
	/// the command changed.
	/// </summary>
	bool TryExecuteCommand(GameCommand command, ulong actor, out CommittedBatch? batch, out Rejection? rejection);
}
