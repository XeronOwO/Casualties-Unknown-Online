using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Runtime.Session.Persistence;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// The item domain's side of a host-side restore: the restored world-item set
/// and the expectation that exactly one of two things ends it — the generation's
/// reconcile (<see cref="Complete"/>) or a cancellation that names the loss
/// (<see cref="Cancel"/>).
///
/// It owns the state so the service that exposes it (<see cref="ItemService"/>,
/// through <see cref="IItemControl"/>) stays a facade: a restore puts the
/// archive's world items into the kernel, the layer is regenerated from the
/// restored baseline, and the objects that generation creates are the SAME
/// physical objects the cut described — so they must be reconciled against these
/// ids instead of published under fresh ones. An armed set is never dropped
/// silently: every exit reports its half of the restore to the audit (§6).
/// </summary>
internal sealed class RestoredWorldItemSet(
	ItemKernelAuthority kernel,
	WorldRestoreAudit? audit,
	ILogger log)
{
	private readonly ItemKernelAuthority _kernel = kernel;
	private readonly WorldRestoreAudit? _audit = audit;
	private readonly ILogger _log = log;

	/// <summary>Host/solo: a restored cut's world items are the truth until the generation reconciles them.</summary>
	internal bool Pending { get; private set; }

	/// <summary>
	/// Which restore attempt armed this set — the kernel restore sequence that produced
	/// it. It rides every report the set makes, so a reconcile that finishes after a
	/// LATER restore opened its own account is attributed to the attempt it belongs to
	/// instead of being counted toward the new one.
	/// </summary>
	private ulong _restoreSequence;

	/// <summary>Host/solo: a restore just put the archive's world items into the kernel.</summary>
	internal void Arm()
	{
		Pending = true;
		_restoreSequence = _kernel.RestoreSequence;
		_log.LogInformation("[Restore] the restored world-item set is armed: the next generation reconciles its objects against it (kernel restore {Sequence}).", _restoreSequence);
	}

	/// <summary>
	/// Host/solo: a restored cut replaced the kernel. Arm the reconcile and rebuild
	/// the world table from the restored set WITHOUT raising the adapter's spawn
	/// events — the live objects belong to the generation reconcile, and
	/// materializing here would race the generation. The arming happens first, so a
	/// failed rebuild still leaves the restore accounted for.
	/// </summary>
	internal void ArmForRestoredCut(GameCheckpoint checkpoint, KernelBatchItemProjection projection)
	{
		Arm();
		projection.RebuildWorldTableOnly(checkpoint.Items);
	}

	/// <summary>The restored world items, in the shape the reconcile lands them.</summary>
	internal IReadOnlyList<WorldItem> Read() =>
		[.. _kernel.QueryItems().Values
			.Where(item => item.Location.Kind == ItemLocationKind.World)
			.Select(KernelBatchItemProjection.ToWorldItem)];

	/// <summary>The generation finished reconciling. A refusal makes the restore incomplete, visibly.</summary>
	internal void Complete(int applied, IReadOnlyList<string> refused)
	{
		if (!Pending)
		{
			return;
		}

		Pending = false;
		var complete = refused.Count == 0;
		_log.LogInformation("[Restore] the generation reconciled the restored item set: {Applied} applied, {Refused} not taken.", applied, refused.Count);
		_audit?.LiveWriteFinished(
			_restoreSequence,
			complete,
			refused,
			complete
				? $"the live world took the restored item set ({applied} entr{(applied == 1 ? "y" : "ies")} bound or materialized)"
				: $"the live world did not take {string.Join(", ", refused)}");
	}

	/// <summary>
	/// The restored set will never be reconciled (a layer-end cut, a new run, the
	/// session ending). An armed set reports the loss; a call with nothing armed is
	/// a no-op — a layer-end cut cancels an expectation it never had.
	/// </summary>
	internal void Cancel(string reason)
	{
		if (!Pending)
		{
			return;
		}

		Pending = false;
		_log.LogWarning("[Restore] the restored item set is dropped without a generation reconcile: {Reason}", reason);
		_audit?.LiveWriteAbandoned(_restoreSequence, reason);
	}
}
