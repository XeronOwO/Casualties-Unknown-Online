using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.WorldEntities;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.ProjectionHealth;
using CasualtiesUnknownOnline.Runtime.Time;
using Microsoft.Extensions.Logging;
using System;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// Projects the kernel WorldEntities checkpoint into the world-entry
/// application surfaces on the guest. When a guest restores a checkpoint it
/// raises the same flat fact lists the Game Adapter already knows how to apply.
/// This is the production rebuild path; the legacy snapshot message ids and
/// handlers have been removed.
/// </summary>
public sealed class WorldEntityKernelProjection
{
	private readonly ItemKernelAuthority _kernelAuthority;
	private readonly ISessionControl _session;
	private readonly ITimeSource _time;
	private readonly ILogger<WorldEntityKernelProjection> _log;
	private readonly ProjectionHealthCoordinator _projectionHealth;

	public WorldEntityKernelProjection(
		ItemKernelAuthority kernelAuthority,
		ISessionControl session,
		ITimeSource time,
		ILogger<WorldEntityKernelProjection> log,
		ProjectionHealthCoordinator projectionHealth)
	{
		_kernelAuthority = kernelAuthority;
		_session = session;
		_time = time;
		_log = log;
		_projectionHealth = projectionHealth;
		_kernelAuthority.CheckpointRestored += OnCheckpointRestored;
		_projectionHealth.Register("world-entities", RebuildFromKernel, () => _kernelAuthority.CurrentGlobalRevision);
	}

	/// <summary>Raised when a restored checkpoint carries trap consumptions.</summary>
	public event Action<IReadOnlyList<EntityEventMsg>>? TrapSnapshotProjected;

	/// <summary>Raised when a restored checkpoint carries opened lockable entities.</summary>
	public event Action<IReadOnlyList<NetVector2Msg>>? OpenedEntitiesProjected;

	/// <summary>Raised when a restored checkpoint carries building-entity health facts.</summary>
	public event Action<IReadOnlyList<BuildingEntityHealthEntryMsg>>? BuildingHealthProjected;

	public void Project(GameCheckpoint checkpoint) =>
		ProjectState(checkpoint.WorldEntities ?? WorldEntityState.Empty, checkpoint.GlobalRevision);

	private void ProjectState(WorldEntityState state, ulong revision)
	{
		if (_session.Role != SessionRole.Guest)
		{
			return;
		}

		var now = _time.NowMs;
		var projected = new List<EntityEventMsg>();
		foreach (var consumption in state.Consumptions)
		{
			projected.Add(new EntityEventMsg
			{
				Kind = (EntityEventKind)consumption.Kind,
				Extra = consumption.Extra,
				Position = new NetVector2Msg(consumption.Position.CenterX, consumption.Position.CenterY),
				ElapsedSeconds = (now - consumption.TriggeredAtMs) / 1000f,
			});
		}

		// One-shot consumptions already cover their terminal presentation. The
		// state table adds the non-one-shot machine facts (warning edges,
		// durable repeatable clamp/heat, ...) for a late joiner. Transient
		// repeatable cooldown presentation (turret shot, geyser eruption) is
		// explicitly skipped: the entity re-arms natively, so re-sending the old
		// fact would replay a stale shot/eruption on every periodic checkpoint.
		foreach (var trapState in state.TrapStates)
		{
			if (EntityEventProfiles.IsOneShotConsumption((EntityEventKind)trapState.Kind)
				|| EntityEventProfiles.IsTransientTrapState((EntityEventKind)trapState.Kind)
				|| trapState.Phase == TrapPhase.Warning)
			{
				// One-shot consumptions already carry their terminal presentation;
				// transient repeatable states are not durable; warning edges are
				// transient and intentionally not snapshotted.
				continue;
			}

			projected.Add(new EntityEventMsg
			{
				Kind = (EntityEventKind)trapState.Kind,
				Extra = trapState.Extra,
				Position = new NetVector2Msg(trapState.Position.CenterX, trapState.Position.CenterY),
				ElapsedSeconds = (now - trapState.TransitionedAtMs) / 1000f,
			});
		}

		if (projected.Count > 0)
		{
			TrapSnapshotProjected?.Invoke(projected);
		}

		if (state.OpenedEntities.Count > 0)
		{
			OpenedEntitiesProjected?.Invoke(
			[
				.. state.OpenedEntities.Select(o => new NetVector2Msg(o.Position.CenterX, o.Position.CenterY)),
			]);
		}

		if (state.BuildingHealth.Count > 0)
		{
			BuildingHealthProjected?.Invoke(
			[
				.. state.BuildingHealth.Select(h => new BuildingEntityHealthEntryMsg
				{
					X = h.Position.CenterX,
					Y = h.Position.CenterY,
					Health = h.Health,
				}),
			]);
		}

		_log.LogDebug(
			"[WorldEntityKernel] projected checkpoint {Revision}: consumptions={Consumptions}, opened={Opened}, health={Health}.",
			revision, state.Consumptions.Count, state.OpenedEntities.Count, state.BuildingHealth.Count);
	}

	private void RebuildFromKernel() =>
		ProjectState(_kernelAuthority.QueryWorldEntities() ?? WorldEntityState.Empty, _kernelAuthority.CurrentGlobalRevision);

	private void OnCheckpointRestored(GameCheckpoint checkpoint) =>
		_projectionHealth.Run("world-entities", checkpoint.GlobalRevision, () => Project(checkpoint));
}
