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
/// application surfaces. When a checkpoint is restored, the SAME facts have to
/// reach the live world, but the moment they can differs per role:
///
/// - a GUEST restores a checkpoint onto a world the host already generated — the
///   scene it stands in IS the restored layer — so the flat fact lists are raised
///   now, and the Game Adapter applies them through its own appliers;
/// - the HOST/SOLO side is mid-continue: the checkpoint was applied at the click,
///   before the scene load, so the only world alive is the layer being REPLACED.
///   The facts are held (<see cref="IRestoredWorldEntitySource"/>) and written by
///   <see cref="RestoredWorldFactReplay"/> at the world-entry seam of the layer
///   the restore regenerates. Applying them any earlier would write this cut's
///   entity state onto the scene it is replacing.
///
/// The mapping itself is one code path (<see cref="BuildFacts"/>) so the guest and
/// the host cannot drift apart. This is the production rebuild path; the legacy
/// snapshot message ids and handlers have been removed.
/// </summary>
public sealed class WorldEntityKernelProjection : IRestoredWorldEntitySource
{
	private readonly ItemKernelAuthority _kernelAuthority;
	private readonly ISessionControl _session;
	private readonly ITimeSource _time;
	private readonly ILogger<WorldEntityKernelProjection> _log;
	private readonly ProjectionHealthCoordinator _projectionHealth;

	/// <summary>Host/solo: the restored cut's world-entity facts, waiting for the world-entry seam. Null everywhere else.</summary>
	private WorldEntityState? _pendingRestore;

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
		_session.SessionEnded += OnSessionEnded;
		_projectionHealth.Register(new ProjectionDomain("world-entities", RebuildFromKernel, () => _kernelAuthority.CurrentGlobalRevision));
	}

	/// <summary>
	/// The session this restore belonged to is gone: the layer its facts describe is
	/// gone with it. An arm that outlived its session would make the next
	/// world-entry seam skip its layer-boundary reset and write a previous session's
	/// facts into a new world, so it is released here with its counts (named, never
	/// silent).
	/// </summary>
	private void OnSessionEnded() =>
		CancelPendingRestore("the session ended before the restored world-entity facts reached the world-entry seam");

	/// <summary>Raised when a restored checkpoint carries trap consumptions.</summary>
	public event Action<IReadOnlyList<EntityEventMsg>>? TrapSnapshotProjected;

	/// <summary>Raised when a restored checkpoint carries opened lockable entities.</summary>
	public event Action<IReadOnlyList<NetVector2Msg>>? OpenedEntitiesProjected;

	/// <summary>Raised when a restored checkpoint carries building-entity health facts.</summary>
	public event Action<IReadOnlyList<BuildingEntityHealthEntryMsg>>? BuildingHealthProjected;

	/// <inheritdoc />
	public bool HasPendingRestore => _pendingRestore is not null;

	/// <inheritdoc />
	public RestoredWorldEntityFacts ReadPendingFacts() =>
		_pendingRestore is { } state ? BuildFacts(state) : RestoredWorldEntityFacts.Empty;

	/// <inheritdoc />
	public void CommitPendingRestore()
	{
		if (_pendingRestore is null)
		{
			return;
		}

		_pendingRestore = null;
		_log.LogInformation("[WorldEntityKernel] the live world took the restored world-entity facts; the pending write is done.");
	}

	/// <inheritdoc />
	public void CancelPendingRestore(string reason)
	{
		if (_pendingRestore is not { } state)
		{
			return;
		}

		_pendingRestore = null;
		_log.LogWarning(
			"[WorldEntityKernel] the restored world-entity facts are dropped without reaching the live world: {Reason} ({Consumptions} consumption(s), {Opened} opened entit(ies), {Health} health row(s)).",
			reason, state.Consumptions.Count, state.OpenedEntities.Count, state.BuildingHealth.Count);
	}

	public void Project(GameCheckpoint checkpoint) =>
		ProjectState(checkpoint.WorldEntities ?? WorldEntityState.Empty, checkpoint.GlobalRevision);

	private void ProjectState(WorldEntityState state, ulong revision)
	{
		if (_session.Role == SessionRole.Guest)
		{
			ProjectNow(state, revision);
			return;
		}

		// Host/solo: the world this cut describes does not exist yet — the restore
		// regenerates it from the restored run baseline — so the facts wait for the
		// world-entry seam instead of being applied to the layer being replaced. An
		// EMPTY projection is not a half to write: arming one would add a "carried
		// nothing" contribution to the restore's account (and, for a layer-end cut, a
		// warning about dropping nothing) for a table with nothing to say about any
		// layer. The gate asks the PROJECTION, not the raw table: the trap-state filter
		// can reduce a non-empty table to no rows at all.
		if (!HasProjectableFact(state))
		{
			_log.LogDebug(
				"[WorldEntityKernel] the restored checkpoint projects no world-entity fact (revision {Revision}); nothing is armed for the world-entry seam.",
				revision);
			return;
		}

		_pendingRestore = state;
		_log.LogInformation(
			"[WorldEntityKernel] the restored world-entity facts are armed for the world-entry seam (revision {Revision}): {Consumptions} consumption(s), {Opened} opened entit(ies), {Health} health row(s).",
			revision, state.Consumptions.Count, state.OpenedEntities.Count, state.BuildingHealth.Count);
	}

	/// <summary>Whether the projection of this table produces any row at all (see <see cref="IsProjectableTrapState"/>).</summary>
	private static bool HasProjectableFact(WorldEntityState state) =>
		state.Consumptions.Count > 0
		|| state.OpenedEntities.Count > 0
		|| state.BuildingHealth.Count > 0
		|| state.TrapStates.Any(IsProjectableTrapState);

	/// <summary>
	/// Whether one trap-state row survives the projection filter: a one-shot
	/// consumption already carries its terminal presentation, a transient repeatable
	/// state (a turret shot, a geyser eruption) is not durable, and a warning edge is
	/// transient — the entity re-arms natively, so re-sending the old fact would replay
	/// a stale shot/eruption on every periodic checkpoint.
	/// </summary>
	private static bool IsProjectableTrapState(TrapStateFact trapState) =>
		!EntityEventProfiles.IsOneShotConsumption((EntityEventKind)trapState.Kind)
		&& !EntityEventProfiles.IsTransientTrapState((EntityEventKind)trapState.Kind)
		&& trapState.Phase != TrapPhase.Warning;

	/// <summary>The guest path: the world the checkpoint describes IS the live one, so the flat lists go out now.</summary>
	private void ProjectNow(WorldEntityState state, ulong revision)
	{
		var facts = BuildFacts(state);
		if (facts.Traps.Count > 0)
		{
			TrapSnapshotProjected?.Invoke(facts.Traps);
		}

		if (facts.Opened.Count > 0)
		{
			OpenedEntitiesProjected?.Invoke(facts.Opened);
		}

		if (facts.Health.Count > 0)
		{
			BuildingHealthProjected?.Invoke(facts.Health);
		}

		_log.LogDebug(
			"[WorldEntityKernel] projected checkpoint {Revision}: consumptions={Consumptions}, opened={Opened}, health={Health}.",
			revision, state.Consumptions.Count, state.OpenedEntities.Count, state.BuildingHealth.Count);
	}

	/// <summary>
	/// The kernel fact table → flat fact lists mapping, one path for both roles.
	/// One-shot consumptions already cover their terminal presentation. The state
	/// table adds the non-one-shot machine facts (warning edges, durable
	/// repeatable clamp/heat, ...) for a late joiner; transient repeatable
	/// cooldown presentation (turret shot, geyser eruption) is explicitly skipped:
	/// the entity re-arms natively, so re-sending the old fact would replay a stale
	/// shot/eruption on every periodic checkpoint.
	/// </summary>
	private RestoredWorldEntityFacts BuildFacts(WorldEntityState state)
	{
		var now = _time.NowMs;
		var traps = new List<EntityEventMsg>();
		foreach (var consumption in state.Consumptions)
		{
			traps.Add(new EntityEventMsg
			{
				Kind = (EntityEventKind)consumption.Kind,
				Extra = consumption.Extra,
				Position = new NetVector2Msg(consumption.Position.CenterX, consumption.Position.CenterY),
				ElapsedSeconds = (now - consumption.TriggeredAtMs) / 1000f,
			});
		}

		foreach (var trapState in state.TrapStates)
		{
			if (!IsProjectableTrapState(trapState))
			{
				continue;
			}

			traps.Add(new EntityEventMsg
			{
				Kind = (EntityEventKind)trapState.Kind,
				Extra = trapState.Extra,
				Position = new NetVector2Msg(trapState.Position.CenterX, trapState.Position.CenterY),
				ElapsedSeconds = (now - trapState.TransitionedAtMs) / 1000f,
			});
		}

		return new RestoredWorldEntityFacts(
			traps,
			state.OpenedEntities.Count == 0
				? []
				: [.. state.OpenedEntities.Select(o => new NetVector2Msg(o.Position.CenterX, o.Position.CenterY))],
			state.BuildingHealth.Count == 0
				? []
				: [
					.. state.BuildingHealth.Select(h => new BuildingEntityHealthEntryMsg
					{
						X = h.Position.CenterX,
						Y = h.Position.CenterY,
						Health = h.Health,
					}),
				]);
	}

	/// <summary>
	/// The projection-health rebuild: the kernel moved and the projection has to
	/// catch up. It is a GUEST repair — only a guest's live world is a projection
	/// of the host's kernel. On the host/solo side the live world IS the authority,
	/// so there is nothing to write and the rebuild must not arm a pending restore
	/// for a layer that was never replaced.
	/// </summary>
	private void RebuildFromKernel()
	{
		if (_session.Role != SessionRole.Guest)
		{
			return;
		}

		ProjectNow(_kernelAuthority.QueryWorldEntities() ?? WorldEntityState.Empty, _kernelAuthority.CurrentGlobalRevision);
	}

	private void OnCheckpointRestored(GameCheckpoint checkpoint) =>
		_projectionHealth.Run("world-entities", checkpoint.GlobalRevision, () => Project(checkpoint));
}
