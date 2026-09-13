namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// Host/solo: the world-entity facts a restored cut put into the kernel and the
/// live world has not taken yet.
///
/// The guest path has no use for this port — a guest's checkpoint restore lands on
/// a world the host already generated, so <see cref="WorldEntityKernelProjection"/>
/// applies the facts immediately. The host/solo restore runs at the Continue click,
/// BEFORE the scene loads: the only world alive at that instant is the layer being
/// replaced, so the restored facts are held here until the world-entry seam of the
/// regenerated layer (the same seam, and the same read-then-commit shape, as the
/// other restored facts — <see cref="RestoredWorldFactReplay"/>).
///
/// An armed set is never dropped silently: the seam either commits it (every row
/// reached the live world) or cancels it with the reason, which is logged with the
/// counts that were lost.
/// </summary>
public interface IRestoredWorldEntitySource
{
	/// <summary>True while a restored cut's world-entity facts are waiting for the world-entry seam.</summary>
	bool HasPendingRestore { get; }

	/// <summary>
	/// WHICH restore attempt armed these facts (the kernel restore sequence that
	/// produced them); 0 while nothing is pending. It is what makes the seam's
	/// contribution attributable: the restore account opened for this sequence is the
	/// only one that may count it, so a write that reaches the seam after a LATER
	/// restore opened its own account cannot raise that account's report early.
	/// </summary>
	ulong PendingRestoreSequence { get; }

	/// <summary>The pending facts in the shape the live world's appliers take. Never called unless <see cref="HasPendingRestore"/>.</summary>
	RestoredWorldEntityFacts ReadPendingFacts();

	/// <summary>The live world took every row: end the pending write.</summary>
	void CommitPendingRestore();

	/// <summary>The pending write will never happen (a refusal, a throw, a new run, a layer-end cut whose facts describe the layer being replaced): end it and name the loss.</summary>
	void CancelPendingRestore(string reason);
}
