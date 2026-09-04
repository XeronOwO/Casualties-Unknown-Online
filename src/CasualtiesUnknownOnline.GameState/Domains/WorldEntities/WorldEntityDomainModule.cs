using System;
using System.Linq;
using CasualtiesUnknownOnline.GameState.Kernel;
using System.Collections.Generic;

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
	private const int MaxTrapStates = 65536;

	public bool CanHandle(GameCommand command) =>
		command is RecordTrapConsumedCommand
			or RecordBuildingEntityHealthCommand
			or RecordOpenedEntityCommand
			or RecordTrapStateCommand
			or ResetWorldEntitiesCommand;

	public bool CanReduce(GameEvent @event) => @event is WorldEntityEvent;

	public DomainDecision Decide(GameCommand command, KernelReadModel state, CommandContext context) =>
		command switch
		{
			RecordTrapConsumedCommand c => DecideRecordTrap(c, state),
			RecordBuildingEntityHealthCommand c => DecideRecordHealth(c, state),
			RecordOpenedEntityCommand c => DecideRecordOpened(c, state),
			RecordTrapStateCommand c => DecideRecordTrapState(c, state),
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
		var current = entities.BuildingHealth.FirstOrDefault(h => h.Position == command.Position);
		if (current is not null && current.Health <= 0f && command.Health > 0f)
		{
			return DomainDecision.Reject(RejectionReason.InvalidTransition,
				"destroyed building entity cannot accept damage or be revived by a health report");
		}

		if (current is null && entities.BuildingHealth.Count >= MaxBuildingHealth)
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

	private static DomainDecision DecideRecordTrapState(RecordTrapStateCommand command, KernelReadModel state)
	{
		var entities = state.WorldEntities ?? WorldEntityState.Empty;
		var current = entities.TrapStates.FirstOrDefault(s => s.Position == command.Position && s.Kind == command.Kind);
		if (current is not null && !IsLegalTrapTransition(current.Phase, command.Phase))
		{
			return DomainDecision.Reject(RejectionReason.InvalidTransition,
				$"trap {command.Kind} at ({command.Position.X},{command.Position.Y}) cannot transition from {current.Phase} to {command.Phase}");
		}

		if (current is null && entities.TrapStates.Count >= MaxTrapStates)
		{
			return DomainDecision.Reject(RejectionReason.Conflict,
				"trap state table is full");
		}

		return DomainDecision.Accept(new TrapStateChangedEvent(
			command.Position,
			command.Kind,
			command.Phase,
			command.Extra,
			command.TransitionedAtMs));
	}

	private static bool IsLegalTrapTransition(TrapPhase current, TrapPhase next) =>
		current == next || (current, next) switch
		{
			(TrapPhase.Armed, TrapPhase.Warning) => true,
			(TrapPhase.Armed, TrapPhase.Triggered) => true,
			(TrapPhase.Armed, TrapPhase.Disabled) => true,
			(TrapPhase.Warning, TrapPhase.Triggered) => true,
			(TrapPhase.Warning, TrapPhase.Disabled) => true,
			(TrapPhase.Triggered, TrapPhase.Cooldown) => true,
			(TrapPhase.Triggered, TrapPhase.Disabled) => true,
			(TrapPhase.Cooldown, TrapPhase.Armed) => true,
			(TrapPhase.Cooldown, TrapPhase.Disabled) => true,
			_ => false,
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
			TrapStateChangedEvent trapState => current.WithTrapState(new TrapStateFact(
				trapState.Position,
				trapState.Kind,
				trapState.Phase,
				trapState.Extra,
				trapState.TransitionedAtMs)),
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

		var states = new HashSet<(EntityPosition Position, int Kind)>();
		foreach (var trapState in entities.TrapStates)
		{
			if (trapState.Kind <= 0)
			{
				throw new InvalidOperationException($"trap state at ({trapState.Position.X},{trapState.Position.Y}) has invalid kind {trapState.Kind}");
			}

			if (!Enum.IsDefined(typeof(TrapPhase), trapState.Phase))
			{
				throw new InvalidOperationException($"trap state at ({trapState.Position.X},{trapState.Position.Y}) has invalid phase {trapState.Phase}");
			}

			if (!states.Add((trapState.Position, trapState.Kind)))
			{
				throw new InvalidOperationException($"trap state at ({trapState.Position.X},{trapState.Position.Y}) kind {trapState.Kind} appears more than once");
			}
		}
	}
}
