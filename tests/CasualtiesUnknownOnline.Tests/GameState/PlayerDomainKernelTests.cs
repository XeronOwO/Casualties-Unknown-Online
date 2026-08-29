using System;
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

	private static Decision Update(GameStateKernel kernel, ulong op, PlayerState state) =>
		kernel.Execute(
			new UpdatePlayerStatusCommand(new OperationId(op), Host, Epoch, AuthorityKind.HostOnly, state),
			new CommandContext(Epoch, Host));
}
