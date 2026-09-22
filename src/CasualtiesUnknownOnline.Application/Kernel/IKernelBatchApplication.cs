using System;
using CasualtiesUnknownOnline.GameState;

namespace CasualtiesUnknownOnline.Application.Kernel;

/// <summary>
/// The receiving half of the kernel replication: apply a committed batch,
/// restore a checkpoint, drop the session's facts when the session ends, and
/// observe the batches this side itself commits (the host broadcasts them).
/// </summary>
public interface IKernelBatchApplication
{
	/// <summary>Raised for every batch this side commits — the host's broadcast trigger.</summary>
	event Action<CommittedBatch>? BatchCommitted;

	/// <summary>Apply a committed batch to the local replay kernel.</summary>
	ApplyResult Apply(CommittedBatch batch);

	/// <summary>Replace the local kernel state with a restored checkpoint.</summary>
	RestoreResult Restore(GameCheckpoint checkpoint);

	/// <summary>The session ended — drop the per-session kernel facts (the run identity and applied-operation memory).</summary>
	void ResetSessionState();
}
