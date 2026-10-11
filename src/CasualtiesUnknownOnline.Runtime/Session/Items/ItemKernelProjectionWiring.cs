using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Application.Kernel;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Session.ProjectionHealth;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// The item domain's kernel-batch wiring: one owner for HOW a committed batch, a
/// restored checkpoint and an arriving item stream reach the item projections,
/// which role sees which half, and for un-wiring all of it again.
///
/// Split out of <see cref="ItemService"/> along the seam the architecture
/// watchlist named (the facade keeps the world-item table and the public
/// <c>IItemControl</c> surface): a host projects the batches it accepted from the
/// peers and rebuilds the carried arbitration table with them, a guest projects
/// the batches it replayed, and the projection-health registration here is what
/// makes a failed projection recoverable from the kernel instead of leaving the
/// domain silently stale.
/// </summary>
internal sealed class ItemKernelProjectionWiring : IDisposable
{
	private readonly ISessionControl _session;
	private readonly ItemKernelAuthority _kernelAuthority;
	private readonly KernelBatchItemProjection _kernelBatchProjection;
	private readonly ItemSnapshotStreamReceiver _streamReceiver;
	private readonly ProjectionHealthCoordinator _projectionHealth;
	private readonly RestoredWorldItemSet _restoredWorldItems;
	private readonly ItemIdCoordinator _idCoordinator;
	private readonly ItemArbitration _arbitration;
	private readonly IKernelProtocolControl _kernelProtocol;
	private readonly Action<IReadOnlyList<WireItemMoveEntry>> _fireItemMoves;

	internal ItemKernelProjectionWiring(
		ISessionControl session,
		ILogger log,
		ItemKernelAuthority kernelAuthority,
		KernelBatchItemProjection kernelBatchProjection,
		ItemSnapshotService snapshots,
		ProjectionHealthCoordinator projectionHealth,
		RestoredWorldItemSet restoredWorldItems,
		ItemIdCoordinator idCoordinator,
		ItemArbitration arbitration,
		IKernelProtocolControl kernelProtocol,
		Action<IReadOnlyList<WireItemMoveEntry>> fireItemMoves)
	{
		_session = session;
		_kernelAuthority = kernelAuthority;
		_kernelBatchProjection = kernelBatchProjection;
		_projectionHealth = projectionHealth;
		_restoredWorldItems = restoredWorldItems;
		_idCoordinator = idCoordinator;
		_arbitration = arbitration;
		_kernelProtocol = kernelProtocol;
		_fireItemMoves = fireItemMoves;
		_streamReceiver = new ItemSnapshotStreamReceiver(
			session,
			kernelAuthority,
			log,
			(items, layerModifierIndex, randomState) => snapshots.FireItemSnapshotReceived(session.HostSteamId, items, layerModifierIndex, randomState),
			(items, layerModifierIndex, randomState) => snapshots.FireWorldItemsSnapshotReceived(session.HostSteamId, items, layerModifierIndex, randomState));

		kernelAuthority.ExternalBatchCommitted += OnExternalBatchCommitted;
		kernelAuthority.BatchApplied += OnBatchApplied;
		kernelAuthority.CheckpointRestored += OnCheckpointRestored;
		kernelProtocol.ItemStateStreamReceived += OnItemStateStreamReceived;
		kernelProtocol.ItemMovesReceived += OnItemMovesReceived;
		projectionHealth.Register(new ProjectionDomain("items", RebuildProjection, () => kernelAuthority.CurrentGlobalRevision));
	}

	public void Dispose()
	{
		_kernelAuthority.ExternalBatchCommitted -= OnExternalBatchCommitted;
		_kernelAuthority.BatchApplied -= OnBatchApplied;
		_kernelAuthority.CheckpointRestored -= OnCheckpointRestored;
		_kernelProtocol.ItemStateStreamReceived -= OnItemStateStreamReceived;
		_kernelProtocol.ItemMovesReceived -= OnItemMovesReceived;
	}

	/// <summary>
	/// A host-originated native transition already wrote the scene: only the
	/// rebuildable world table must converge, and without raising the adapter
	/// events a peer's replay would raise.
	/// </summary>
	internal void ApplyWorldTableOnly(CommittedBatch batch) => _kernelBatchProjection.ApplyWorldTableOnly(batch);

	/// <summary>Session ended: a half-received stream must not be resumed into the next lobby.</summary>
	internal void ResetStream() => _streamReceiver.Reset();

	/// <summary>Host/solo: the accepted batch is projected here as well — the host's own arbitration table is a projection of the same kernel the guests replay.</summary>
	private void OnExternalBatchCommitted(CommittedBatch batch)
	{
		if (_session.Role != SessionRole.Host)
		{
			return;
		}

		_projectionHealth.Run("items", batch.GlobalRevision, () =>
		{
			_kernelBatchProjection.Apply(batch);
			_arbitration.RebuildCarriedTableFromKernel();
		});
	}

	/// <summary>Guest: a batch broadcast by the host was replayed into the local kernel — the items and the heater conversion it carries are surfaced here.</summary>
	private void OnBatchApplied(CommittedBatch batch)
	{
		if (_session.Role != SessionRole.Guest)
		{
			return;
		}

		_projectionHealth.Run("items", batch.GlobalRevision, () =>
		{
			_kernelBatchProjection.Apply(batch);
			_kernelBatchProjection.FireCookedEventFromBatch(batch);
		});
	}

	private void OnCheckpointRestored(GameCheckpoint checkpoint)
	{
		if (_session.Role != SessionRole.Guest)
		{
			// Host/solo: the archive's world items ARE this world's item set — the layer
			// that follows is regenerated from the restored baseline, so the generation
			// reconciles its objects against these ids (RestoredWorldItemSet). A
			// layer-end cut never gets here: the restore applier cancels the expectation.
			//
			// The same rows are the restore's id fact: each names an instance id of the
			// form (counter, SteamId), i.e. an id the PREVIOUS process handed out, while
			// this process's allocator starts at zero. Resuming every SteamId's counter
			// above what the archive re-issued happens HERE, before the generation below
			// can allocate (its publish is suppressed for a restored cut, but the next
			// layer's is not) and outside the projection-health scope: it is a session
			// fact, not a projection of the kernel.
			_idCoordinator.ResumeRestoredIds(checkpoint.Items.Select(item => item.Identity.InstanceId));
			_projectionHealth.Run("items", checkpoint.GlobalRevision, () => _restoredWorldItems.ArmForRestoredCut(checkpoint, _kernelBatchProjection));
			return;
		}

		_projectionHealth.Run("items", checkpoint.GlobalRevision, () => _kernelBatchProjection.Rebuild(checkpoint));
	}

	/// <summary>The projection-health recovery path: rebuild from the live kernel read model, and on a host re-derive the arbitration table with it.</summary>
	private void RebuildProjection()
	{
		_kernelBatchProjection.RebuildFromKernel();
		if (_session.Role == SessionRole.Host)
		{
			_arbitration.RebuildCarriedTableFromKernel();
		}
	}

	private void OnItemMovesReceived(IReadOnlyList<WireItemMoveEntry> moves)
	{
		if (_session.Role != SessionRole.Guest)
		{
			return;
		}

		_fireItemMoves(moves);
	}

	private void OnItemStateStreamReceived(WirePayloadType payloadType, WireStateStream stream) =>
		_streamReceiver.Handle(_session.HostSteamId, payloadType, stream);
}
