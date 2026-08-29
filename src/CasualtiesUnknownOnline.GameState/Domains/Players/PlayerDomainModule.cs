using System;
using CasualtiesUnknownOnline.GameState.Kernel;

namespace CasualtiesUnknownOnline.GameState.Domains.Players;

/// <summary>
/// Players domain module: terminal player status (alive/conscious) and reset.
/// High-frequency motion remains outside the kernel.
/// </summary>
internal sealed class PlayerDomainModule : IDomainModule
{
	public bool CanHandle(GameCommand command) =>
		command is UpdatePlayerStatusCommand or ResetPlayersCommand;

	public bool CanReduce(GameEvent @event) => @event is PlayerEvent;

	public DomainDecision Decide(GameCommand command, KernelReadModel state, CommandContext context) =>
		command switch
		{
			UpdatePlayerStatusCommand c => DomainDecision.Accept(new PlayerStatusUpdatedEvent(c.State)),
			ResetPlayersCommand => DomainDecision.Accept(new PlayersResetEvent()),
			_ => DomainDecision.Reject(RejectionReason.UnknownCommand, $"unknown player command {command.GetType().Name}"),
		};

	public void Reduce(GameEvent @event, MutableKernelState state)
	{
		var current = state.Players ?? PlayerStateTable.Empty;
		state.Players = @event switch
		{
			PlayerStatusUpdatedEvent updated => current.Upsert(updated.State),
			PlayersResetEvent => PlayerStateTable.Empty,
			_ => throw new InvalidOperationException($"unknown player event {@event.GetType().Name}"),
		};
	}

	public void AssertInvariants(KernelReadModel state)
	{
		if (state.Players is not { } players)
		{
			return;
		}

		var seen = new System.Collections.Generic.HashSet<ulong>();
		foreach (var player in players.Players)
		{
			if (!seen.Add(player.SteamId))
			{
				throw new InvalidOperationException($"player {player.SteamId} appears more than once");
			}

			if (!player.Alive && player.Conscious)
			{
				throw new InvalidOperationException($"dead player {player.SteamId} cannot be conscious");
			}
		}
	}
}
