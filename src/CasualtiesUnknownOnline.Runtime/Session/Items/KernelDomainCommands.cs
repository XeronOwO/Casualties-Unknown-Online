using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Entities;
using CasualtiesUnknownOnline.GameState.Domains.Fluids;
using CasualtiesUnknownOnline.GameState.Domains.Players;
using CasualtiesUnknownOnline.GameState.Domains.World;
using CasualtiesUnknownOnline.GameState.Domains.WorldEntities;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// The kernel's command surface for the domains <see cref="ItemKernelAuthority"/>
/// does NOT own — run, world entities (traps/buildings), players, enemies and
/// fluids. Each member builds one typed command and commits it through the
/// authority's execution seam, so the authority itself keeps what its own
/// contract names (the kernel, the run epoch, the operation counter and the item
/// domain's fact surface) instead of carrying four other domains' entry points.
///
/// They are extension members rather than a collaborator object because the call
/// sites are the domain services themselves (<c>WorldRunProjection</c>,
/// <c>EnemyKernelProjection</c>, <c>FluidKernelProjection</c>, …): the entry point
/// stays where those services already look for it, and nothing about the command,
/// its label or its commit path changes.
/// </summary>
public static class KernelDomainCommands
{
	extension(ItemKernelAuthority authority)
	{
		// ===== World / Run =====

		public bool TryStartRun(ulong actor, RunState run, out CommittedBatch? batch, out Rejection? rejection)
		{
			var command = new StartRunCommand(
				authority.NextOperationId(),
				new ActorId(actor),
				authority.CurrentRunEpoch,
				AuthorityKind.HostOnly,
				run);
			return authority.TryExecuteHostCommand(command, actor, "start-run", out batch, out rejection);
		}

		public bool TryAdvanceLayer(ulong actor, RunState run, out CommittedBatch? batch, out Rejection? rejection)
		{
			var command = new AdvanceLayerCommand(
				authority.NextOperationId(),
				new ActorId(actor),
				authority.CurrentRunEpoch,
				AuthorityKind.HostOnly,
				run);
			return authority.TryExecuteHostCommand(command, actor, "advance-layer", out batch, out rejection);
		}

		// ===== World entities (traps/buildings) =====

		public bool TryRecordTrapConsumed(ulong actor, EntityPosition position, int kind, byte extra, long triggeredAtMs, out CommittedBatch? batch, out Rejection? rejection)
		{
			var command = new RecordTrapConsumedCommand(
				authority.NextOperationId(),
				new ActorId(actor),
				authority.CurrentRunEpoch,
				AuthorityKind.HostOnly,
				position,
				kind,
				extra,
				triggeredAtMs);
			return authority.TryExecuteHostCommand(command, actor, "record-trap-consumed", out batch, out rejection);
		}

		public bool TryRecordBuildingEntityHealth(ulong actor, EntityPosition position, float health, out CommittedBatch? batch, out Rejection? rejection)
		{
			var command = new RecordBuildingEntityHealthCommand(
				authority.NextOperationId(),
				new ActorId(actor),
				authority.CurrentRunEpoch,
				AuthorityKind.HostOnly,
				position,
				health);
			return authority.TryExecuteHostCommand(command, actor, "record-building-health", out batch, out rejection);
		}

		public bool TryRecordOpenedEntity(ulong actor, EntityPosition position, out CommittedBatch? batch, out Rejection? rejection)
		{
			var command = new RecordOpenedEntityCommand(
				authority.NextOperationId(),
				new ActorId(actor),
				authority.CurrentRunEpoch,
				AuthorityKind.HostOnly,
				position);
			return authority.TryExecuteHostCommand(command, actor, "record-opened-entity", out batch, out rejection);
		}

		public bool TryResetWorldEntities(ulong actor, out CommittedBatch? batch, out Rejection? rejection)
		{
			var command = new ResetWorldEntitiesCommand(
				authority.NextOperationId(),
				new ActorId(actor),
				authority.CurrentRunEpoch,
				AuthorityKind.HostOnly);
			return authority.TryExecuteHostCommand(command, actor, "reset-world-entities", out batch, out rejection);
		}

		// ===== Players =====

		public bool TryUpdatePlayerStatus(ulong actor, PlayerState state, out CommittedBatch? batch, out Rejection? rejection) =>
			authority.TryExecuteHostCommand(new UpdatePlayerStatusCommand(authority.NextOperationId(), new ActorId(actor), authority.CurrentRunEpoch, AuthorityKind.HostOnly, state), actor, "update-player-status", out batch, out rejection);

		// The player table has NO reset, and that is a design statement rather than an
		// omission: PlayerState carries the durable CROSS-LAYER facts (alive/conscious,
		// the carry relation, the limb latches, body state, skills — see PlayerState), so
		// a layer boundary must keep them. The family that DOES reset at a boundary is
		// enumerated in WorldService.ResetWorldLayerTables; players are not in it.

		public bool TrySetPlayerCarry(ulong actor, ulong carrierSteamId, ulong carriedSteamId, out CommittedBatch? batch, out Rejection? rejection) =>
			authority.TryExecuteHostCommand(new SetPlayerCarryCommand(authority.NextOperationId(), new ActorId(actor), authority.CurrentRunEpoch, AuthorityKind.HostOnly, carrierSteamId, carriedSteamId), actor, "set-player-carry", out batch, out rejection);

		public bool TryClearPlayerCarry(ulong actor, ulong carrierSteamId, ulong carriedSteamId, out CommittedBatch? batch, out Rejection? rejection) =>
			authority.TryExecuteHostCommand(new ClearPlayerCarryCommand(authority.NextOperationId(), new ActorId(actor), authority.CurrentRunEpoch, AuthorityKind.HostOnly, carrierSteamId, carriedSteamId), actor, "clear-player-carry", out batch, out rejection);

		// ===== Entities =====

		public bool TryUpsertEnemy(ulong actor, EnemyState state, out CommittedBatch? batch, out Rejection? rejection)
		{
			var command = new UpsertEnemyCommand(
				authority.NextOperationId(),
				new ActorId(actor),
				authority.CurrentRunEpoch,
				AuthorityKind.HostOnly,
				state);
			return authority.TryExecuteHostCommand(command, actor, "upsert-enemy", out batch, out rejection);
		}

		public bool TryRemoveEnemy(ulong actor, EntityId entityId, out CommittedBatch? batch, out Rejection? rejection)
		{
			var command = new RemoveEnemyCommand(
				authority.NextOperationId(),
				new ActorId(actor),
				authority.CurrentRunEpoch,
				AuthorityKind.HostOnly,
				entityId);
			return authority.TryExecuteHostCommand(command, actor, "remove-enemy", out batch, out rejection);
		}

		/// <summary>
		/// Host only: the kernel's LAYER-SCOPED enemy table drops its LIVE rows for the layer
		/// being entered. A live enemy row is a fact about the layout it stands in AND about
		/// the id the host allocated for it (<c>EnemySyncCoordinator</c> keeps allocating from
		/// a per-session counter), so a row that survived the boundary would describe an enemy
		/// of a layer the world no longer is — a fact that leaks to a late joiner's checkpoint
		/// and whose id can be re-minted for a different enemy.
		///
		/// The TOMBSTONES survive: <c>EnemyStateTable.WithoutLiveEnemies</c> states why.
		///
		/// Host-local by construction, like the world-item and world-entity resets: a remotely
		/// triggerable "wipe every enemy" is a destructive trigger no peer may have, so this
		/// command has no wire form (<c>KernelWireMapper</c> does not map it) and the
		/// guests converge from the committed batch and the checkpoints.
		///
		/// <paramref name="actor"/> is the LOCAL peer — the host acting as itself. It is a
		/// parameter rather than a session lookup because this authority is also driven by
		/// the save layer, which is not the session.
		/// </summary>
		public bool TryResetEnemies(ulong actor, out CommittedBatch? batch, out Rejection? rejection)
		{
			var command = new ResetEnemiesCommand(
				authority.NextOperationId(),
				new ActorId(actor),
				authority.CurrentRunEpoch,
				AuthorityKind.HostOnly);
			return authority.TryExecuteHostCommand(command, actor, "reset-enemies", out batch, out rejection);
		}

		// ===== Fluids =====

		public bool TryUpdateFluidRegion(ulong actor, FluidRegionState state, out CommittedBatch? batch, out Rejection? rejection)
		{
			var command = new UpdateFluidRegionCommand(
				authority.NextOperationId(),
				new ActorId(actor),
				authority.CurrentRunEpoch,
				AuthorityKind.HostOnly,
				state);
			return authority.TryExecuteHostCommand(command, actor, "update-fluid-region", out batch, out rejection);
		}

		/// <inheritdoc cref="TryResetEnemies"/>
		public bool TryResetFluids(ulong actor, out CommittedBatch? batch, out Rejection? rejection)
		{
			var command = new ResetFluidsCommand(
				authority.NextOperationId(),
				new ActorId(actor),
				authority.CurrentRunEpoch,
				AuthorityKind.HostOnly);
			return authority.TryExecuteHostCommand(command, actor, "reset-fluids", out batch, out rejection);
		}
	}
}
