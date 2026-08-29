using System;
using CasualtiesUnknownOnline.GameState.Kernel;

namespace CasualtiesUnknownOnline.GameState.Domains.WorldEntities;

/// <summary>
/// WorldEntities domain module: one-shot trap consumptions, building-entity
/// health, and opened lockable entities. All facts are position-keyed to the
/// deterministic world-cell identity.
/// </summary>
internal sealed class WorldEntityDomainModule : IDomainModule
{
	public bool CanHandle(GameCommand command) =>
		command is RecordTrapConsumedCommand or RecordBuildingEntityHealthCommand or RecordOpenedEntityCommand;

	public bool CanReduce(GameEvent @event) => @event is WorldEntityEvent;

	public DomainDecision Decide(GameCommand command, KernelReadModel state, CommandContext context) =>
		command switch
		{
			RecordTrapConsumedCommand c => DomainDecision.Accept(new TrapConsumedEvent(c.Position, c.Kind, c.Extra, c.TriggeredAtMs)),
			RecordBuildingEntityHealthCommand c => DomainDecision.Accept(new BuildingEntityHealthUpdatedEvent(c.Position, c.Health)),
			RecordOpenedEntityCommand c => DomainDecision.Accept(new OpenedEntityEvent(c.Position)),
			_ => DomainDecision.Reject(RejectionReason.UnknownCommand, $"unknown world-entity command {command.GetType().Name}"),
		};

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
