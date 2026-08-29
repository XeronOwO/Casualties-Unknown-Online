using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameState.Domains.Entities;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.GameState.Domains.Players;
using CasualtiesUnknownOnline.GameState.Domains.World;
using CasualtiesUnknownOnline.GameState.Domains.WorldEntities;
using CasualtiesUnknownOnline.GameState.Kernel;

namespace CasualtiesUnknownOnline.GameState;

/// <summary>
/// Minimal deterministic kernel. It routes commands to domain modules, creates
/// a working copy, reduces event drafts, validates invariants, assigns the
/// global revision, and atomically swaps the store.
/// </summary>
public sealed class GameStateKernel(RunEpoch runEpoch) : IGameStateKernel
{
	private readonly GameStateStore _store = new(runEpoch);
	private readonly IReadOnlyList<IDomainModule> _modules = [new ItemDomainModule(), new WorldDomainModule(), new WorldEntityDomainModule(), new PlayerDomainModule(), new EnemyDomainModule()];

	public Decision Execute(GameCommand command, CommandContext context)
	{
		if (_store.Operations.TryGet(command.OperationId, out var original))
		{
			return Decision.Accepted(original);
		}

		if (command.RunEpoch != _store.RunEpoch || context.RunEpoch != _store.RunEpoch)
		{
			return Decision.Rejected(Rejection.Of(RejectionReason.WrongEpoch,
				$"command epoch {command.RunEpoch.Value} does not match kernel epoch {_store.RunEpoch.Value}"));
		}

		var module = _modules.FirstOrDefault(m => m.CanHandle(command));
		if (module is null)
		{
			return Decision.Rejected(Rejection.Of(RejectionReason.UnknownCommand,
				$"no domain module handles {command.GetType().Name}"));
		}

		var decision = module.Decide(command, new KernelReadModel(_store.RunEpoch, _store.GlobalRevision, _store.Items, _store.Run, _store.WorldEntities, _store.Players, _store.Enemies), context);
		if (!decision.Accepted)
		{
			return Decision.Rejected(decision.Rejection!);
		}

		var working = _store.CreateWorkingCopy();
		foreach (var @event in decision.Events)
		{
			module.Reduce(@event, working);
		}

		try
		{
			module.AssertInvariants(new KernelReadModel(working.RunEpoch, working.GlobalRevision, working.Items, working.Run, working.WorldEntities, working.Players, working.Enemies));
		}
		catch (InvalidOperationException e)
		{
			return Decision.Rejected(Rejection.Of(RejectionReason.InvariantViolation, e.Message));
		}

		working.GlobalRevision = _store.GlobalRevision + 1;
		var batch = new CommittedBatch(
			command.OperationId,
			working.GlobalRevision,
			command.Actor,
			command.Authority,
			command.RunEpoch,
			command.Preconditions,
			[.. decision.Events]);

		_store.ReplaceWith(working);
		_store.Operations.Add(command.OperationId, batch);
		return Decision.Accepted(batch);
	}

	public ApplyResult Apply(CommittedBatch batch)
	{
		if (_store.Operations.TryGet(batch.OperationId, out _))
		{
			return ApplyResult.Ok();
		}

		if (batch.RunEpoch != _store.RunEpoch)
		{
			return ApplyResult.Failed($"batch epoch {batch.RunEpoch.Value} does not match kernel epoch {_store.RunEpoch.Value}");
		}

		var working = _store.CreateWorkingCopy();
		foreach (var @event in batch.Events)
		{
			var module = _modules.FirstOrDefault(m => m.CanReduce(@event));
			if (module is null)
			{
				return ApplyResult.Failed($"no domain module reduces {@event.GetType().Name}");
			}

			module.Reduce(@event, working);
		}

		try
		{
			foreach (var module in _modules)
			{
				module.AssertInvariants(new KernelReadModel(working.RunEpoch, working.GlobalRevision, working.Items, working.Run, working.WorldEntities, working.Players, working.Enemies));
			}
		}
		catch (InvalidOperationException e)
		{
			return ApplyResult.Failed(e.Message);
		}

		working.GlobalRevision = batch.GlobalRevision;
		_store.ReplaceWith(working);
		_store.Operations.Add(batch.OperationId, batch);
		return ApplyResult.Ok();
	}

	public GameCheckpoint CreateCheckpoint() => _store.CreateCheckpoint();

	public RestoreResult Restore(GameCheckpoint checkpoint)
	{
		_store.Restore(checkpoint);
		return RestoreResult.Ok();
	}

	public IReadOnlyDictionary<ulong, ItemState> QueryItems() => _store.Items;

	public ItemState? FindItem(ulong instanceId) =>
		_store.Items.TryGetValue(instanceId, out var item) ? item : null;

	public RunState? QueryRun() => _store.Run;

	public WorldEntityState? QueryWorldEntities() => _store.WorldEntities;

	public PlayerStateTable? QueryPlayers() => _store.Players;

	public EnemyStateTable? QueryEnemies() => _store.Enemies;
}
