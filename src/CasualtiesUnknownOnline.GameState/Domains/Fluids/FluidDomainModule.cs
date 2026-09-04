using System;
using CasualtiesUnknownOnline.GameState.Kernel;
using System.Collections.Generic;

namespace CasualtiesUnknownOnline.GameState.Domains.Fluids;

/// <summary>
/// Fluids domain module: persistent region checkpoints and reset.
/// </summary>
internal sealed class FluidDomainModule : IDomainModule
{
	public bool CanHandle(GameCommand command) =>
		command is UpdateFluidRegionCommand or ResetFluidsCommand;

	public bool CanReduce(GameEvent @event) => @event is FluidEvent;

	public DomainDecision Decide(GameCommand command, KernelReadModel state, CommandContext context) =>
		command switch
		{
			UpdateFluidRegionCommand c => DomainDecision.Accept(new FluidRegionUpdatedEvent(c.State)),
			ResetFluidsCommand => DomainDecision.Accept(new FluidsResetEvent()),
			_ => DomainDecision.Reject(RejectionReason.UnknownCommand, $"unknown fluid command {command.GetType().Name}"),
		};

	public void Reduce(GameEvent @event, MutableKernelState state)
	{
		var current = state.Fluids ?? FluidStateTable.Empty;
		state.Fluids = @event switch
		{
			FluidRegionUpdatedEvent updated => current.Upsert(updated.State),
			FluidsResetEvent => FluidStateTable.Empty,
			_ => throw new InvalidOperationException($"unknown fluid event {@event.GetType().Name}"),
		};
	}

	public void AssertInvariants(KernelReadModel state)
	{
		if (state.Fluids is not { } fluids)
		{
			return;
		}

		var seen = new HashSet<(int, int)>();
		foreach (var region in fluids.Regions)
		{
			if (!seen.Add((region.ChunkX, region.ChunkY)))
			{
				throw new InvalidOperationException($"fluid region ({region.ChunkX},{region.ChunkY}) appears more than once");
			}

			if (region.TotalAmount < 0)
			{
				throw new InvalidOperationException($"fluid region ({region.ChunkX},{region.ChunkY}) has invalid total {region.TotalAmount}");
			}
		}
	}
}
