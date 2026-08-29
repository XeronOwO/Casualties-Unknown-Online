using System;
using System.Linq;
using CasualtiesUnknownOnline.GameState.Kernel;

namespace CasualtiesUnknownOnline.GameState.Domains.WorldEntities;

/// <summary>
/// WorldEntities domain module: one-shot trap consumptions, building-entity
/// health, and opened lockable entities. All facts are position-keyed to the
/// deterministic world-cell identity.
/// </summary>
internal sealed class WorldEntityDomainModule : IDomainModule
{
	private const int MaxConsumptions = 65536;
	private const int MaxBuildingHealth = 4096;
	private const int MaxOpenedEntities = 4096;

	public bool CanHandle(GameCommand command) =>
		command is RecordTrapConsumedCommand or RecordBuildingEntityHealthCommand or RecordOpenedEntityCommand or ResetWorldEntitiesCommand;

	public bool CanReduce(GameEvent @event) => @event is WorldEntityEvent;

	public DomainDecision Decide(GameCommand command, KernelReadModel state, CommandContext context) =>
		command switch
		{
			RecordTrapConsumedCommand c => DecideRecordTrap(c, state),
			RecordBuildingEntityHealthCommand c => DecideRecordHealth(c, state),
			RecordOpenedEntityCommand c => DecideRecordOpened(c, state),
			ResetWorldEntitiesCommand => DomainDecision.Accept(new WorldEntitiesResetEvent()),
			_ => DomainDecision.Reject(RejectionReason.UnknownCommand, $"unknown world-entity command {command.GetType().Name}"),
		};

	private static DomainDecision DecideRecordTrap(RecordTrapConsumedCommand command, KernelReadModel state)
	{
		var entities = state.WorldEntities ?? WorldEntityState.Empty;
		var exists = entities.Consumptions.Any(c => c.Position == command.Position);
		if (!exists && entities.Consumptions.Count >= MaxConsumptions)
		{
			return DomainDecision.Reject(RejectionReason.Conflict,
				"trap consumption table is full");
		}

		return DomainDecision.Accept(new TrapConsumedEvent(command.Position, command.Kind, command.Extra, command.TriggeredAtMs));
	}

	private static DomainDecision DecideRecordHealth(RecordBuildingEntityHealthCommand command, KernelReadModel state)
	{
		var entities = state.WorldEntities ?? WorldEntityState.Empty;
		var exists = entities.BuildingHealth.Any(h => h.Position == command.Position);
		if (!exists && entities.BuildingHealth.Count >= MaxBuildingHealth)
		{
			return DomainDecision.Reject(RejectionReason.Conflict,
				"building-entity health table is full");
		}

		return DomainDecision.Accept(new BuildingEntityHealthUpdatedEvent(command.Position, command.Health));
	}

	private static DomainDecision DecideRecordOpened(RecordOpenedEntityCommand command, KernelReadModel state)
	{
		var entities = state.WorldEntities ?? WorldEntityState.Empty;
		var exists = entities.OpenedEntities.Any(o => o.Position == command.Position);
		if (!exists && entities.OpenedEntities.Count >= MaxOpenedEntities)
		{
			return DomainDecision.Reject(RejectionReason.Conflict,
				"opened-entity table is full");
		}

		return DomainDecision.Accept(new OpenedEntityEvent(command.Position));
	}

	public void Reduce(GameEvent @event, MutableKernelState state)
	{
		var current = state.WorldEntities ?? WorldEntityState.Empty;
		state.WorldEntities = @event switch
		{
			TrapConsumedEvent consumed => current.WithConsumption(new TrapConsumptionFact(
				consumed.Position,
				consumed.Kind,
				consumed.Extra,
				consumed.TriggeredAtMs)),
			BuildingEntityHealthUpdatedEvent health => current.WithBuildingHealth(new BuildingEntityHealthFact(
				health.Position,
				health.Health)),
			OpenedEntityEvent opened => current.WithOpened(new OpenedEntityFact(opened.Position)),
			WorldEntitiesResetEvent => WorldEntityState.Empty,
			_ => throw new InvalidOperationException($"unknown world-entity event {@event.GetType().Name}"),
		};
	}

	public void AssertInvariants(KernelReadModel state)
	{
		if (state.WorldEntities is not { } entities)
		{
			return;
		}

		foreach (var consumption in entities.Consumptions)
		{
			if (consumption.Kind <= 0)
			{
				throw new InvalidOperationException($"trap consumption at ({consumption.Position.X},{consumption.Position.Y}) has invalid kind {consumption.Kind}");
			}
		}

		foreach (var health in entities.BuildingHealth)
		{
			if (float.IsNaN(health.Health) || health.Health < 0f)
			{
				throw new InvalidOperationException($"building entity at ({health.Position.X},{health.Position.Y}) has invalid health {health.Health}");
			}
		}
	}
}
