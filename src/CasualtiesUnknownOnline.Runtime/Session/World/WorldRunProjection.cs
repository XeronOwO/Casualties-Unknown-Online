using System;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.World;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.ProjectionHealth;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The world service's run-baseline projection: committing the kernel run
/// baseline when the run parameters are published (run start, or the layer
/// advance that follows it) and projecting the kernel's run facts back into
/// world parameters on every batch, restore or rebuild. Split out of
/// <see cref="WorldService"/> at the 600-line aggregate gate — the run
/// projection is one cohesive responsibility with its own kernel events and its
/// own projection-health domain, and it reads/writes exactly one service surface
/// (<c>WorldParams</c>, through <paramref name="applyWorldParams"/>).
/// </summary>
internal sealed class WorldRunProjection(
	ISessionControl session,
	ItemKernelAuthority kernelAuthority,
	ProjectionHealthCoordinator projectionHealth,
	Action<WorldStartParams?> applyWorldParams,
	ILogger<WorldService> log)
{
	/// <summary>Host/solo: publish the run parameters as the kernel baseline — the first publish starts the run, a later one advances the layer (a regeneration).</summary>
	internal void CommitBaseline(WorldStartParams parameters)
	{
		var current = kernelAuthority.QueryRun();
		var runId = current?.RunId ?? kernelAuthority.CreateCheckpoint().RunEpoch.Value;
		var layerIndex = current is null ? 0 : current.LayerIndex + 1;
		var run = WorldRunStateMapper.ToRunState(runId, parameters, layerIndex);

		if (current is null)
		{
			if (!kernelAuthority.TryStartRun(session.LocalSteamId, run, out _, out var rejection))
			{
				log.LogWarning("Kernel run start rejected: {Reason} ({Message}).", rejection!.Reason, rejection.Message);
				return;
			}

			log.LogInformation("Committed kernel run start (run {RunId}, {StateBytes} RNG bytes).",
				runId, parameters.RandomState.Length);
			return;
		}

		if (!kernelAuthority.TryAdvanceLayer(session.LocalSteamId, run, out _, out var advanceRejection))
		{
			log.LogWarning("Kernel layer advance rejected: {Reason} ({Message}).", advanceRejection!.Reason, advanceRejection.Message);
			return;
		}

		log.LogInformation("Committed kernel layer advance (run {RunId}, layer {Layer}).", runId, layerIndex);
	}

	/// <summary>A committed or applied batch carries the run facts — project them.</summary>
	internal void OnBatch(CommittedBatch batch)
	{
		projectionHealth.Run("run", batch.GlobalRevision, () =>
		{
			foreach (var @event in batch.Events)
			{
				switch (@event)
				{
					case RunStartedEvent started:
						Apply(started.Run);
						break;
					case RunAdvancedEvent advanced:
						Apply(advanced.Run);
						break;
				}
			}
		});
	}

	/// <summary>A restored checkpoint carries the run facts — project them.</summary>
	internal void OnCheckpointRestored(GameCheckpoint checkpoint)
	{
		projectionHealth.Run("run", checkpoint.GlobalRevision, () =>
		{
			if (checkpoint.Run is not null)
			{
				Apply(checkpoint.Run);
			}
		});
	}

	/// <summary>The projection was rebuilt from the kernel — re-apply the run facts (or clear them when the kernel holds no run).</summary>
	internal void Rebuild(Action clearWorldParams)
	{
		var run = kernelAuthority.QueryRun();
		if (run is not null)
		{
			Apply(run);
		}
		else
		{
			clearWorldParams();
			log.LogInformation("[RunProjection] kernel run is null; cleared the world-start projection.");
		}

		log.LogDebug("[RunProjection] rebuilt from kernel at revision {Revision}.", kernelAuthority.CurrentGlobalRevision);
	}

	private void Apply(RunState run)
	{
		applyWorldParams(WorldRunStateMapper.ToWorldStartParams(run));
		log.LogInformation("Projected kernel run baseline (run {RunId}, layer {Layer}, {StateBytes} RNG bytes).",
			run.RunId, run.LayerIndex, run.RandomState.Length);
	}
}
