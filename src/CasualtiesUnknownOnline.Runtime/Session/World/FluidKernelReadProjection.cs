using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Fluids;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.ProjectionHealth;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// Guest-side rebuildable read projection of the kernel fluid-region table.
/// It mirrors the authoritative coarse fluid facts from checkpoint restore and
/// applied kernel batches, giving guest consumers a deterministic rebuild path
/// separate from the high-frequency RLE grid stream.
/// </summary>
public sealed class FluidKernelReadProjection : IDisposable
{
	private readonly ItemKernelAuthority _kernelAuthority;
	private readonly ISessionControl _session;
	private readonly ILogger<FluidKernelReadProjection> _log;
	private readonly ProjectionHealthCoordinator _projectionHealth;
	private List<FluidRegionState> _regions = [];

	public FluidKernelReadProjection(
		ItemKernelAuthority kernelAuthority,
		ISessionControl session,
		ILogger<FluidKernelReadProjection> log,
		ProjectionHealthCoordinator projectionHealth)
	{
		_kernelAuthority = kernelAuthority;
		_session = session;
		_log = log;
		_projectionHealth = projectionHealth;
		_kernelAuthority.BatchApplied += OnBatchApplied;
		_kernelAuthority.CheckpointRestored += OnCheckpointRestored;
		_projectionHealth.Register("fluids", RebuildFromKernel, () => _kernelAuthority.CurrentGlobalRevision);
	}

	/// <summary>The current rebuilt fluid-region snapshot.</summary>
	public IReadOnlyList<FluidRegionState> Regions => _regions;

	/// <summary>Raised after a checkpoint restore or applied batch rebuilds the fluid-region snapshot.</summary>
	public event Action<IReadOnlyList<FluidRegionState>>? RegionsProjected;

	public void Dispose()
	{
		_kernelAuthority.BatchApplied -= OnBatchApplied;
		_kernelAuthority.CheckpointRestored -= OnCheckpointRestored;
	}

	private void OnBatchApplied(CommittedBatch batch)
	{
		if (_session.Role != SessionRole.Guest)
		{
			return;
		}

		_projectionHealth.Run("fluids", batch.GlobalRevision, () =>
		{
			var changed = false;
			foreach (var @event in batch.Events)
			{
				switch (@event)
				{
					case FluidRegionUpdatedEvent updated:
						Upsert(updated.State);
						changed = true;
						break;
					case FluidsResetEvent:
						_regions.Clear();
						changed = true;
						break;
				}
			}

			if (changed)
			{
				Raise(batch.GlobalRevision);
			}
		});
	}

	private void OnCheckpointRestored(GameCheckpoint checkpoint)
	{
		if (_session.Role != SessionRole.Guest)
		{
			return;
		}

		_projectionHealth.Run("fluids", checkpoint.GlobalRevision, () =>
		{
			_regions = [.. checkpoint.Fluids?.Regions ?? []];
			Raise(checkpoint.GlobalRevision);
		});
	}

	private void RebuildFromKernel()
	{
		_regions = [.. _kernelAuthority.QueryFluids()?.Regions ?? []];
		Raise(_kernelAuthority.CurrentGlobalRevision);
	}

	private void Upsert(FluidRegionState state)
	{
		_regions =
		[
			.. _regions.Where(r => r.ChunkX != state.ChunkX || r.ChunkY != state.ChunkY),
			state,
		];
	}

	private void Raise(ulong revision)
	{
		RegionsProjected?.Invoke(_regions);
		_log.LogDebug(
			"[FluidKernelRead] rebuilt guest fluid regions at revision {Revision}: {Count} region(s).",
			revision, _regions.Count);
	}
}
