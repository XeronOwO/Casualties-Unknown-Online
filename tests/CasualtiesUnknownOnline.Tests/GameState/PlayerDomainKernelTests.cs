using System;
using System.Linq;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Players;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.GameState;

public class PlayerDomainKernelTests
{
	private static readonly RunEpoch Epoch = new(1);
	private static readonly ActorId Host = new(1001);

	[Fact]
	public void UpdateStatus_UpsertsPlayerTableAndCheckpoint()
	{
		var kernel = new GameStateKernel(Epoch);
		Assert.True(Update(kernel, 1, new PlayerState(2001, true, true)).IsAccepted);
		Assert.True(Update(kernel, 2, new PlayerState(2001, false, false)).IsAccepted);

		var state = kernel.QueryPlayers();
		Assert.NotNull(state);
		var player = Assert.Single(state!.Players);
		Assert.Equal(2001ul, player.SteamId);
		Assert.False(player.Alive);
		Assert.False(player.Conscious);

		var checkpoint = kernel.CreateCheckpoint();
		var restored = new GameStateKernel(new RunEpoch(99));
		Assert.True(restored.Restore(checkpoint).Success);
		Assert.Equal(2001ul, Assert.Single(restored.QueryPlayers()!.Players).SteamId);
	}

	[Fact]
	public void DeadConsciousPlayer_IsRejectedByInvariant()
	{
		var kernel = new GameStateKernel(Epoch);

		var decision = Update(kernel, 1, new PlayerState(2001, false, true));

		Assert.False(decision.IsAccepted);
		Assert.Equal(RejectionReason.InvariantViolation, decision.Rejection!.Reason);
	}

	[Fact]
	public void ResetPlayers_ClearsTable()
	{
		var kernel = new GameStateKernel(Epoch);
		Assert.True(Update(kernel, 1, new PlayerState(2001, true, true)).IsAccepted);
		Assert.True(kernel.Execute(
			new ResetPlayersCommand(new OperationId(2), Host, Epoch, AuthorityKind.HostOnly),
			new CommandContext(Epoch, Host)).IsAccepted);

		Assert.Empty(kernel.QueryPlayers()!.Players);
	}

	[Fact]
	public void UpdateStatus_UpsertsLimbFacts()
	{
		var kernel = new GameStateKernel(Epoch);
		Assert.True(Update(kernel, 1, new PlayerState(
			2001,
			true,
			true,
			Limbs:
			[
				new PlayerLimbState(0, Broken: true, Dismembered: false, Dislocated: false, Splinted: false, Infected: false, BlockedBleeding: false, IsHead: true, IsVital: false),
			])).IsAccepted);

		var player = Assert.Single(kernel.QueryPlayers()!.Players);
		var limb = Assert.Single(player.LimbFacts);
		Assert.True(limb.Broken);
		Assert.True(limb.IsHead);
	}

	[Fact]
	public void DuplicateLimbIndex_IsRejectedByInvariant()
	{
		var kernel = new GameStateKernel(Epoch);
		var decision = Update(kernel, 1, new PlayerState(
			2001,
			true,
			true,
			Limbs:
			[
				new PlayerLimbState(0, false, false, false, false, false, false, false, false),
				new PlayerLimbState(0, false, false, false, false, false, false, false, false),
			]));

		Assert.False(decision.IsAccepted);
		Assert.Equal(RejectionReason.InvariantViolation, decision.Rejection!.Reason);
	}

	[Fact]
	public void WireBatchRoundTrip_PreservesPlayerLimbFacts()
	{
		var source = new GameStateKernel(Epoch);
		var batch = Update(source, 1, new PlayerState(
			2001,
			true,
			true,
			Limbs:
			[
				new PlayerLimbState(2, Broken: true, Dismembered: false, Dislocated: false, Splinted: false, Infected: false, BlockedBleeding: false, IsHead: false, IsVital: true),
			])).Batch!;

		var restored = KernelWireMapper.FromWireBatch(KernelWireMapper.ToWireBatch(batch), Epoch);

		var @event = Assert.IsType<PlayerStatusUpdatedEvent>(Assert.Single(restored.Events));
		var limb = Assert.Single(@event.State.LimbFacts);
		Assert.Equal(2, limb.Index);
		Assert.True(limb.Broken);
		Assert.True(limb.IsVital);
	}

	[Fact]
	public void CheckpointSplitAssemble_RoundTripsPlayerLimbFacts()
	{
		var kernel = new GameStateKernel(Epoch);
		Assert.True(Update(kernel, 1, new PlayerState(
			2001,
			true,
			true,
			Limbs:
			[
				new PlayerLimbState(1, Broken: false, Dismembered: true, Dislocated: false, Splinted: false, Infected: false, BlockedBleeding: false, IsHead: false, IsVital: false),
			])).IsAccepted);

		var restored = WireCheckpointAssembler.Assemble(WireCheckpointAssembler.Split(kernel.CreateCheckpoint()));

		var player = Assert.Single(restored.Players!.Players);
		var limb = Assert.Single(player.LimbFacts);
		Assert.True(limb.Dismembered);
	}

	[Fact]
	public void SaveLoad_RoundTripsPlayerLimbFacts()
	{
		var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cuo-players-limbs-{Guid.NewGuid():N}.bin");
		try
		{
			var authority = new ItemKernelAuthority(NullLogger<ItemKernelAuthority>.Instance);
			Assert.True(authority.TryUpdatePlayerStatus(
				Host.Value,
				new PlayerState(
					2001,
					true,
					true,
					Limbs:
					[
						new PlayerLimbState(3, Broken: true, Dismembered: false, Dislocated: true, Splinted: false, Infected: true, BlockedBleeding: true, IsHead: false, IsVital: false),
					]),
				out _,
				out _));

			var store = new KernelSaveFileStore(path, NullLogger<KernelSaveFileStore>.Instance);
			Assert.True(store.Save(authority.CreateCheckpoint()));
			Assert.True(store.TryLoad(out var loaded));

			var player = Assert.Single(loaded.Players!.Players);
			var limb = Assert.Single(player.LimbFacts);
			Assert.True(limb.Broken);
			Assert.True(limb.Dislocated);
			Assert.True(limb.Infected);
			Assert.True(limb.BlockedBleeding);
		}
		finally
		{
			if (System.IO.File.Exists(path))
			{
				System.IO.File.Delete(path);
			}
		}
	}

	[Fact]
	public void SetAndClearCarry_DrivePlayerRelation()
	{
		var kernel = new GameStateKernel(Epoch);
		Assert.True(Update(kernel, 1, new PlayerState(2001, true, true)).IsAccepted);
		Assert.True(Update(kernel, 2, new PlayerState(2002, true, true)).IsAccepted);

		Assert.True(SetCarry(kernel, 3, 2001, 2002).IsAccepted);

		var players = kernel.QueryPlayers()!.Players;
		var carrier = players.Single(p => p.SteamId == 2001);
		var carried = players.Single(p => p.SteamId == 2002);
		Assert.Equal(2002ul, carrier.CarrierOfSteamId);
		Assert.Equal(2001ul, carried.CarriedBySteamId);

		Assert.True(ClearCarry(kernel, 4, 2001, 2002).IsAccepted);
		players = kernel.QueryPlayers()!.Players;
		Assert.Null(players.Single(p => p.SteamId == 2001).CarrierOfSteamId);
		Assert.Null(players.Single(p => p.SteamId == 2002).CarriedBySteamId);
	}

	[Fact]
	public void SelfCarry_IsRejected()
	{
		var kernel = new GameStateKernel(Epoch);
		Assert.True(Update(kernel, 1, new PlayerState(2001, true, true)).IsAccepted);

		var decision = SetCarry(kernel, 2, 2001, 2001);

		Assert.False(decision.IsAccepted);
		Assert.Equal(RejectionReason.InvalidTransition, decision.Rejection!.Reason);
	}

	[Fact]
	public void CarryConflict_IsRejected()
	{
		var kernel = new GameStateKernel(Epoch);
		Assert.True(Update(kernel, 1, new PlayerState(2001, true, true)).IsAccepted);
		Assert.True(Update(kernel, 2, new PlayerState(2002, true, true)).IsAccepted);
		Assert.True(Update(kernel, 3, new PlayerState(2003, true, true)).IsAccepted);
		Assert.True(SetCarry(kernel, 4, 2001, 2002).IsAccepted);

		var decision = SetCarry(kernel, 5, 2001, 2003);

		Assert.False(decision.IsAccepted);
		Assert.Equal(RejectionReason.Conflict, decision.Rejection!.Reason);
	}

	[Fact]
	public void WireBatchRoundTrip_PreservesPlayerStatusEvent()
	{
		var source = new GameStateKernel(Epoch);
		var batch = Update(source, 1, new PlayerState(2001, true, false)).Batch!;

		var restored = KernelWireMapper.FromWireBatch(KernelWireMapper.ToWireBatch(batch), Epoch);

		var @event = Assert.IsType<PlayerStatusUpdatedEvent>(Assert.Single(restored.Events));
		Assert.Equal(2001ul, @event.State.SteamId);
		Assert.True(@event.State.Alive);
		Assert.False(@event.State.Conscious);
	}

	[Fact]
	public void CheckpointSplitAssemble_RoundTripsPlayers()
	{
		var kernel = new GameStateKernel(Epoch);
		Assert.True(Update(kernel, 1, new PlayerState(2001, true, true)).IsAccepted);

		var restored = WireCheckpointAssembler.Assemble(WireCheckpointAssembler.Split(kernel.CreateCheckpoint()));

		var player = Assert.Single(restored.Players!.Players);
		Assert.Equal(2001ul, player.SteamId);
		Assert.True(player.Alive);
	}

	[Fact]
	public void SaveLoad_RoundTripsPlayers()
	{
		var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cuo-players-{Guid.NewGuid():N}.bin");
		try
		{
			var authority = new ItemKernelAuthority(NullLogger<ItemKernelAuthority>.Instance);
			Assert.True(authority.TryUpdatePlayerStatus(Host.Value, new PlayerState(2001, true, true), out _, out _));

			var store = new KernelSaveFileStore(path, NullLogger<KernelSaveFileStore>.Instance);
			Assert.True(store.Save(authority.CreateCheckpoint()));
			Assert.True(store.TryLoad(out var loaded));

			var player = Assert.Single(loaded.Players!.Players);
			Assert.Equal(2001ul, player.SteamId);
			Assert.True(player.Conscious);
		}
		finally
		{
			if (System.IO.File.Exists(path))
			{
				System.IO.File.Delete(path);
			}
		}
	}

	[Fact]
	public void WireBatchRoundTrip_PreservesPlayerCarryEvent()
	{
		var source = new GameStateKernel(Epoch);
		Assert.True(Update(source, 1, new PlayerState(2001, true, true)).IsAccepted);
		Assert.True(Update(source, 2, new PlayerState(2002, true, true)).IsAccepted);
		var batch = SetCarry(source, 3, 2001, 2002).Batch!;

		var restored = KernelWireMapper.FromWireBatch(KernelWireMapper.ToWireBatch(batch), Epoch);

		var @event = Assert.IsType<PlayerCarrySetEvent>(Assert.Single(restored.Events));
		Assert.Equal(2001ul, @event.CarrierSteamId);
		Assert.Equal(2002ul, @event.CarriedSteamId);
	}

	[Fact]
	public void CheckpointSplitAssemble_RoundTripsPlayerCarryFields()
	{
		var kernel = new GameStateKernel(Epoch);
		Assert.True(Update(kernel, 1, new PlayerState(2001, true, true)).IsAccepted);
		Assert.True(Update(kernel, 2, new PlayerState(2002, true, true)).IsAccepted);
		Assert.True(SetCarry(kernel, 3, 2001, 2002).IsAccepted);

		var restored = WireCheckpointAssembler.Assemble(WireCheckpointAssembler.Split(kernel.CreateCheckpoint()));

		var carrier = restored.Players!.Players.Single(p => p.SteamId == 2001);
		Assert.Equal(2002ul, carrier.CarrierOfSteamId);
		Assert.Null(carrier.CarriedBySteamId);
	}

	[Fact]
	public void SaveLoad_RoundTripsPlayerCarryFields()
	{
		var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cuo-players-carry-{Guid.NewGuid():N}.bin");
		try
		{
			var authority = new ItemKernelAuthority(NullLogger<ItemKernelAuthority>.Instance);
			Assert.True(authority.TryUpdatePlayerStatus(Host.Value, new PlayerState(2001, true, true), out _, out _));
			Assert.True(authority.TryUpdatePlayerStatus(Host.Value, new PlayerState(2002, true, true), out _, out _));
			Assert.True(authority.TrySetPlayerCarry(Host.Value, 2001, 2002, out _, out _));

			var store = new KernelSaveFileStore(path, NullLogger<KernelSaveFileStore>.Instance);
			Assert.True(store.Save(authority.CreateCheckpoint()));
			Assert.True(store.TryLoad(out var loaded));

			var carrier = loaded.Players!.Players.Single(p => p.SteamId == 2001);
			Assert.Equal(2002ul, carrier.CarrierOfSteamId);
			Assert.Null(carrier.CarriedBySteamId);
		}
		finally
		{
			if (System.IO.File.Exists(path))
			{
				System.IO.File.Delete(path);
			}
		}
	}

	private static Decision Update(GameStateKernel kernel, ulong op, PlayerState state) =>
		kernel.Execute(
			new UpdatePlayerStatusCommand(new OperationId(op), Host, Epoch, AuthorityKind.HostOnly, state),
			new CommandContext(Epoch, Host));

	private static Decision SetCarry(GameStateKernel kernel, ulong op, ulong carrier, ulong carried) =>
		kernel.Execute(
			new SetPlayerCarryCommand(new OperationId(op), Host, Epoch, AuthorityKind.HostOnly, carrier, carried),
			new CommandContext(Epoch, Host));

	private static Decision ClearCarry(GameStateKernel kernel, ulong op, ulong carrier, ulong carried) =>
		kernel.Execute(
			new ClearPlayerCarryCommand(new OperationId(op), Host, Epoch, AuthorityKind.HostOnly, carrier, carried),
			new CommandContext(Epoch, Host));
}
