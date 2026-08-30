using System;
using CasualtiesUnknownOnline.GameState.Kernel;

namespace CasualtiesUnknownOnline.GameState.Domains.Entities;

/// <summary>
/// Entities domain module: enemy/entity lifecycle and health facts.
/// </summary>
internal sealed class EnemyDomainModule : IDomainModule
{
	public bool CanHandle(GameCommand command) =>
		command is UpsertEnemyCommand
			or RemoveEnemyCommand
			or ResetEnemiesCommand
			or RecordEnemyBiteCommand
			or RecordEnemyLungeCommand
			or RecordEnemyEffectCommand;

	public bool CanReduce(GameEvent @event) => @event is EnemyEvent;

	public DomainDecision Decide(GameCommand command, KernelReadModel state, CommandContext context) =>
		command switch
		{
			UpsertEnemyCommand c => DecideUpsert(c, state),
			RemoveEnemyCommand c => DomainDecision.Accept(new EnemyRemovedEvent(c.EntityId)),
			ResetEnemiesCommand => DomainDecision.Accept(new EnemiesResetEvent()),
			RecordEnemyBiteCommand c => DomainDecision.Accept(new EnemyBiteResultEvent(
				c.VictimSteamId,
				c.Limb,
				c.VenomTotal,
				c.Adrenaline,
				c.Happiness)),
			RecordEnemyLungeCommand c => DomainDecision.Accept(new EnemyLungeResultEvent(
				c.VictimSteamId,
				c.Limb,
				c.Adrenaline,
				c.Stamina)),
			RecordEnemyEffectCommand c => DomainDecision.Accept(new EnemyEffectResultEvent(
				c.VictimSteamId,
				c.Kind,
				c.HorrifiedLevel,
				c.FocusedLevel,
				c.Adrenaline,
				c.Energy,
				c.Stamina,
				c.Happiness,
				c.Caffeinated,
				c.SepticShock,
				c.Shock,
				c.EyePanicTime)),
			_ => DomainDecision.Reject(RejectionReason.UnknownCommand, $"unknown entity command {command.GetType().Name}"),
		};

	private static DomainDecision DecideUpsert(UpsertEnemyCommand command, KernelReadModel state)
	{
		var entities = state.Enemies ?? EnemyStateTable.Empty;
		if (entities.IsRemoved(command.State.EntityId))
		{
			return DomainDecision.Reject(RejectionReason.InvalidTransition,
				$"destroyed enemy {command.State.EntityId} cannot be resurrected");
		}

		return DomainDecision.Accept(new EnemyUpsertedEvent(command.State));
	}

	public void Reduce(GameEvent @event, MutableKernelState state)
	{
		var current = state.Enemies ?? EnemyStateTable.Empty;
		state.Enemies = @event switch
		{
			EnemyUpsertedEvent upserted => current.Upsert(upserted.State),
			EnemyRemovedEvent removed => current.Remove(removed.EntityId),
			EnemiesResetEvent => EnemyStateTable.Empty,
			EnemyBiteResultEvent or EnemyLungeResultEvent or EnemyEffectResultEvent => current,
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

		var removedSeen = new System.Collections.Generic.HashSet<EntityId>();
		foreach (var removed in entities.Removed)
		{
			if (seen.Contains(removed))
			{
				throw new InvalidOperationException($"removed entity {removed} still appears in the live table");
			}

			if (!removedSeen.Add(removed))
			{
				throw new InvalidOperationException($"removed entity {removed} appears more than once");
			}
		}
	}
}
