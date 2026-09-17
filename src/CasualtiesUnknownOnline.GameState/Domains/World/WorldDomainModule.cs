using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.GameState.Kernel;

namespace CasualtiesUnknownOnline.GameState.Domains.World;

/// <summary>
/// World/Run/Epoch domain module. It owns the run baseline (identity, random
/// stream state, world-defining fields and typed run settings) and the terminal
/// separation between "no run", "run started", and "layer advanced". Reducers
/// are deterministic and never read ambient state.
/// </summary>
internal sealed class WorldDomainModule : IDomainModule
{
	public bool CanHandle(GameCommand command) => command is StartRunCommand or AdvanceLayerCommand;

	public bool CanReduce(GameEvent @event) => @event is RunEvent;

	public DomainDecision Decide(GameCommand command, KernelReadModel state, CommandContext context) =>
		command switch
		{
			StartRunCommand start => DecideStartRun(start, state),
			AdvanceLayerCommand advance => DecideAdvanceLayer(advance, state),
			_ => DomainDecision.Reject(RejectionReason.UnknownCommand, $"unknown world command {command.GetType().Name}"),
		};

	public void Reduce(GameEvent @event, MutableKernelState state)
	{
		switch (@event)
		{
			case RunStartedEvent started:
				state.SetRun(started.Run);
				break;
			case RunAdvancedEvent advanced:
				state.SetRun(advanced.Run);
				break;
			default:
				throw new InvalidOperationException($"unknown world event {@event.GetType().Name}");
		}
	}

	public void AssertInvariants(KernelReadModel state)
	{
		if (state.Run is not { } run)
		{
			return;
		}

		if (run.RandomState is null || run.RandomState.Length == 0)
		{
			throw new InvalidOperationException("run must carry a non-empty Random.state baseline");
		}

		if (run.LayerIndex < 0)
		{
			throw new InvalidOperationException($"run {run.RunId} has negative layer index {run.LayerIndex}");
		}

		if (run.RunSettings is not null)
		{
			var keys = new HashSet<string>(StringComparer.Ordinal);
			foreach (var setting in run.RunSettings)
			{
				if (!keys.Add(setting.Key))
				{
					throw new InvalidOperationException($"run {run.RunId} contains duplicate setting '{setting.Key}'");
				}
			}
		}

		// The two rarity multipliers are world-generation INPUTS: the layer's loot and
		// trap distribution are scaled by them, so a non-finite value is not a value
		// the game could have produced and must never shape a layer. This assertion
		// covers the two paths that go through a domain module — the host's own capture
		// (the command path rejects the baseline with RejectionReason.InvariantViolation)
		// and the guest's wire checkpoint (the batch fails to apply) — both by name,
		// instead of committing a run no peer can generate from. The RESTORE family does
		// not pass through here at all and is refused at its own seam, before the store
		// is replaced (GameStateKernel.Restore).
		if (!RunRarityMultipliers.IsWellFormed(run.LootRarityMultiplier) || !RunRarityMultipliers.IsWellFormed(run.TrapRarityMultiplier))
		{
			throw new InvalidOperationException(
				$"run {run.RunId} carries a non-finite rarity multiplier (loot {run.LootRarityMultiplier}, trap {run.TrapRarityMultiplier})");
		}
	}

	private static DomainDecision DecideStartRun(StartRunCommand command, KernelReadModel state)
	{
		if (state.Run is not null)
		{
			return DomainDecision.Reject(RejectionReason.Conflict,
				$"run {state.Run.RunId} already started; a run can start once per kernel epoch");
		}

		return DomainDecision.Accept(new RunStartedEvent(command.Run));
	}

	private static DomainDecision DecideAdvanceLayer(AdvanceLayerCommand command, KernelReadModel state)
	{
		if (state.Run is null)
		{
			return DomainDecision.Reject(RejectionReason.UnknownAggregate,
				"cannot advance a layer before a run has started");
		}

		return DomainDecision.Accept(new RunAdvancedEvent(command.Run));
	}
}
