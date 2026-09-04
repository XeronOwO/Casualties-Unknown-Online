using System;
using System.Linq;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.GameState.Kernel;
using System.Collections.Generic;

namespace CasualtiesUnknownOnline.GameState.Domains.Players;

/// <summary>
/// Players domain module: terminal player status (alive/conscious), the
/// cross-player carry relation, and reset. High-frequency motion remains
/// outside the kernel.
/// </summary>
internal sealed class PlayerDomainModule : IDomainModule
{
	public bool CanHandle(GameCommand command) =>
		command is UpdatePlayerStatusCommand
			or ResetPlayersCommand
			or SetPlayerCarryCommand
			or ClearPlayerCarryCommand
			or RecordPlayerInventoryTransferCommand
			or RecordPlayerHealResultCommand
			or RecordPlayerItemUseResultCommand;

	public bool CanReduce(GameEvent @event) => @event is PlayerEvent;

	public DomainDecision Decide(GameCommand command, KernelReadModel state, CommandContext context) =>
		command switch
		{
			UpdatePlayerStatusCommand c => DomainDecision.Accept(new PlayerStatusUpdatedEvent(c.State)),
			ResetPlayersCommand => DomainDecision.Accept(new PlayersResetEvent()),
			SetPlayerCarryCommand c => DecideSetCarry(c, state),
			ClearPlayerCarryCommand c => DomainDecision.Accept(new PlayerCarryClearedEvent(c.CarrierSteamId, c.CarriedSteamId)),
			RecordPlayerInventoryTransferCommand c => DomainDecision.Accept(new PlayerInventoryTransferEvent(c.FromSteamId, c.ToSteamId, c.Item)),
			RecordPlayerHealResultCommand c => DomainDecision.Accept(new PlayerHealResultEvent(
				c.HealerSteamId,
				c.TargetSteamId,
				c.ItemInstanceId,
				c.ItemDestroyed,
				c.ItemConditionAfter,
				c.HealedLimbIndex,
				c.Health,
				c.Limbs)),
			RecordPlayerItemUseResultCommand c => DomainDecision.Accept(new PlayerItemUseResultEvent(
				c.UserSteamId,
				c.TargetSteamId,
				c.ItemInstanceId,
				c.ItemDestroyed,
				c.ItemAfter,
				c.WornItem,
				c.Health,
				c.Limbs,
				c.TimedEffects,
				c.TimedBodyEffects)),
			_ => DomainDecision.Reject(RejectionReason.UnknownCommand, $"unknown player command {command.GetType().Name}"),
		};

	public void Reduce(GameEvent @event, MutableKernelState state)
	{
		var current = state.Players ?? PlayerStateTable.Empty;
		state.Players = @event switch
		{
			PlayerStatusUpdatedEvent updated => current.Upsert(updated.State),
			PlayersResetEvent => PlayerStateTable.Empty,
			PlayerCarrySetEvent carry => ApplyCarry(current, carry.CarrierSteamId, carry.CarriedSteamId),
			PlayerCarryClearedEvent carry => ClearCarry(current, carry.CarrierSteamId, carry.CarriedSteamId),
			PlayerInventoryTransferEvent or PlayerHealResultEvent or PlayerItemUseResultEvent => current,
			_ => throw new InvalidOperationException($"unknown player event {@event.GetType().Name}"),
		};
	}

	public void AssertInvariants(KernelReadModel state)
	{
		if (state.Players is not { } players)
		{
			return;
		}

		var seen = new HashSet<ulong>();
		var byId = players.Players.ToDictionary(p => p.SteamId);
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

			if (player.Limbs is { } limbs)
			{
				var limbIndices = new HashSet<int>();
				foreach (var limb in limbs)
				{
					if (limb.Index < 0)
					{
						throw new InvalidOperationException($"player {player.SteamId} has a negative limb index {limb.Index}");
					}

					if (!limbIndices.Add(limb.Index))
					{
						throw new InvalidOperationException($"player {player.SteamId} has duplicate limb index {limb.Index}");
					}
				}
			}

			AssertCarryFields(player, byId);
		}

		foreach (var item in state.Items.Values)
		{
			if (item.Location.Kind == ItemLocationKind.Carried
				&& item.Location.Owner.Value != 0
				&& !byId.ContainsKey(item.Location.Owner.Value))
			{
				throw new InvalidOperationException(
					$"carried item {item.Identity.InstanceId} references unknown player {item.Location.Owner.Value}");
			}
		}
	}

	private static DomainDecision DecideSetCarry(SetPlayerCarryCommand command, KernelReadModel state)
	{
		if (command.CarrierSteamId == 0 || command.CarriedSteamId == 0 || command.CarrierSteamId == command.CarriedSteamId)
		{
			return DomainDecision.Reject(RejectionReason.InvalidTransition, "carrier and carried must be distinct non-zero players");
		}

		if (state.Players is not { } players)
		{
			return DomainDecision.Reject(RejectionReason.UnknownAggregate, "no player table exists");
		}

		if (!players.Players.Any(p => p.SteamId == command.CarrierSteamId)
			|| !players.Players.Any(p => p.SteamId == command.CarriedSteamId))
		{
			return DomainDecision.Reject(RejectionReason.UnknownAggregate, $"carry references a player that does not exist ({command.CarrierSteamId} -> {command.CarriedSteamId})");
		}

		if (players.Players.Any(p => (p.SteamId == command.CarrierSteamId || p.SteamId == command.CarriedSteamId)
			&& (p.CarrierOfSteamId is not null || p.CarriedBySteamId is not null)))
		{
			return DomainDecision.Reject(RejectionReason.Conflict, "carrier or carried player is already in a carry relation");
		}

		return DomainDecision.Accept(new PlayerCarrySetEvent(command.CarrierSteamId, command.CarriedSteamId));
	}

	private static PlayerStateTable ApplyCarry(PlayerStateTable table, ulong carrier, ulong carried) =>
		table with
		{
			Players = [.. table.Players.Select(p =>
				p.SteamId == carried
					? p.WithCarry(null, carrier)
					: p.SteamId == carrier
						? p.WithCarry(carried, null)
						: p)],
		};

	private static PlayerStateTable ClearCarry(PlayerStateTable table, ulong carrier, ulong carried)
	{
		var current = table.Players.FirstOrDefault(p => p.SteamId == carrier);
		var actualCarried = carried != 0 ? carried : current?.CarrierOfSteamId ?? 0;
		if (current is null || current.CarrierOfSteamId != actualCarried)
		{
			return table;
		}

		return table with
		{
			Players = [.. table.Players.Select(p =>
				p.SteamId == carrier || p.SteamId == actualCarried
					? p.WithCarry(null, null)
					: p)],
		};
	}

	private static void AssertCarryFields(
		PlayerState player,
		Dictionary<ulong, PlayerState> byId)
	{
		if (player.CarrierOfSteamId is { } carriedSteamId)
		{
			if (carriedSteamId == player.SteamId)
			{
				throw new InvalidOperationException($"player {player.SteamId} cannot carry themself");
			}

			if (!player.Alive || !player.Conscious)
			{
				throw new InvalidOperationException($"player {player.SteamId} is not alive/conscious and cannot carry {carriedSteamId}");
			}

			if (player.CarriedBySteamId is not null)
			{
				throw new InvalidOperationException($"player {player.SteamId} cannot be both carrier and carried");
			}

			if (!byId.TryGetValue(carriedSteamId, out var carried))
			{
				throw new InvalidOperationException($"player {player.SteamId} carries unknown player {carriedSteamId}");
			}

			if (carried.CarriedBySteamId != player.SteamId)
			{
				throw new InvalidOperationException($"carry relation is not reciprocal for {player.SteamId} -> {carriedSteamId}");
			}
		}

		if (player.CarriedBySteamId is { } carrierSteamId)
		{
			if (carrierSteamId == player.SteamId)
			{
				throw new InvalidOperationException($"player {player.SteamId} cannot be carried by themself");
			}

			if (player.CarrierOfSteamId is not null)
			{
				throw new InvalidOperationException($"player {player.SteamId} cannot be both carrier and carried");
			}

			if (!byId.TryGetValue(carrierSteamId, out var carrier))
			{
				throw new InvalidOperationException($"player {player.SteamId} is carried by unknown player {carrierSteamId}");
			}

			if (carrier.CarrierOfSteamId != player.SteamId)
			{
				throw new InvalidOperationException($"carry relation is not reciprocal for {carrierSteamId} -> {player.SteamId}");
			}
		}
	}
}
