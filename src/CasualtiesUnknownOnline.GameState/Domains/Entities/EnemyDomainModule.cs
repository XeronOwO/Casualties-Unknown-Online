using System;
using CasualtiesUnknownOnline.GameState.Kernel;

namespace CasualtiesUnknownOnline.GameState.Domains.Entities;

/// <summary>
/// Entities domain module: enemy/entity lifecycle and health facts.
/// </summary>
internal sealed class EnemyDomainModule : IDomainModule
{
	public bool CanHandle(GameCommand command) =>
		command is UpsertEnemyCommand or RemoveEnemyCommand or ResetEnemiesCommand;

	public bool CanReduce(GameEvent @event) => @event is EnemyEvent;

	public DomainDecision Decide(GameCommand command, KernelReadModel state, CommandContext context) =>
		command switch
		{
			UpsertEnemyCommand c => DomainDecision.Accept(new EnemyUpsertedEvent(c.State)),
			RemoveEnemyCommand c => DomainDecision.Accept(new EnemyRemovedEvent(c.EntityId)),
			ResetEnemiesCommand => DomainDecision.Accept(new EnemiesResetEvent()),
			_ => DomainDecision.Reject(RejectionReason.UnknownCommand, $"unknown entity command {command.GetType().Name}"),
		};

	public void Reduce(GameEvent @event, MutableKernelState state)
	{
		var current = state.Enemies ?? EnemyStateTable.Empty;
		state.Enemies = @event switch
		{
			EnemyUpsertedEvent upserted => current.Upsert(upserted.State),
			EnemyRemovedEvent removed => current.Remove(removed.EntityId),
			EnemiesResetEvent => EnemyStateTable.Empty,
			_ => throw new InvalidOperationException($"unknown entity event {@event.GetType().Name}"),
		};
	}

	public void AssertInvariants(KernelReadModel state)
	{
		if (state.Enemies is not { } entities)
		{
			return;
		}

		var seen = new System.Collections.Generic.HashSet<EntityId>();
		foreach (var enemy in entities.Enemies)
		{
			if (!seen.Add(enemy.EntityId))
			{
				throw new InvalidOperationException($"entity {enemy.EntityId} appears more than once");
			}

			if (float.IsNaN(enemy.Health) || enemy.Health < 0f)
			{
				throw new InvalidOperationException($"entity {enemy.EntityId} has invalid health {enemy.Health}");
			}
		}
	}
}
