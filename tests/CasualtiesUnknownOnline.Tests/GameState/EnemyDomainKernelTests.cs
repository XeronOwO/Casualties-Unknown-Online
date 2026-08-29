using System;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Entities;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.GameState;

public class EnemyDomainKernelTests
{
	private static readonly RunEpoch Epoch = new(1);
	private static readonly ActorId Host = new(1001);
	private static readonly EntityId EnemyId = new(1, 2, 0);

	[Fact]
	public void UpsertRemoveAndReset_DriveEnemyTable()
	{
		var kernel = new GameStateKernel(Epoch);
		Assert.True(Upsert(kernel, 1, new EnemyState(EnemyId, "spider", 10f, false, false)).IsAccepted);
		Assert.True(Upsert(kernel, 2, new EnemyState(EnemyId, "spider", 5f, false, true)).IsAccepted);

		var enemy = Assert.Single(kernel.QueryEnemies()!.Enemies);
		Assert.Equal(5f, enemy.Health);
		Assert.True(enemy.Stunned);

		Assert.True(kernel.Execute(
			new RemoveEnemyCommand(new OperationId(3), Host, Epoch, AuthorityKind.HostOnly, EnemyId),
			new CommandContext(Epoch, Host)).IsAccepted);
		Assert.Empty(kernel.QueryEnemies()!.Enemies);

		Assert.True(Upsert(kernel, 4, new EnemyState(EnemyId, "crystal", 3f, true, false)).IsAccepted);
		Assert.True(kernel.Execute(
			new ResetEnemiesCommand(new OperationId(5), Host, Epoch, AuthorityKind.HostOnly),
			new CommandContext(Epoch, Host)).IsAccepted);
		Assert.Empty(kernel.QueryEnemies()!.Enemies);
	}

	[Fact]
	public void NegativeHealth_IsRejectedByInvariant()
	{
		var kernel = new GameStateKernel(Epoch);

		var decision = Upsert(kernel, 1, new EnemyState(EnemyId, "spider", -1f, false, false));

		Assert.False(decision.IsAccepted);
		Assert.Equal(RejectionReason.InvariantViolation, decision.Rejection!.Reason);
	}

	[Fact]
	public void WireBatchRoundTrip_PreservesEnemyUpsertedEvent()
	{
		var source = new GameStateKernel(Epoch);
		var batch = Upsert(source, 1, new EnemyState(EnemyId, "crystal", 7f, true, false)).Batch!;

		var restored = KernelWireMapper.FromWireBatch(KernelWireMapper.ToWireBatch(batch), Epoch);

		var @event = Assert.IsType<EnemyUpsertedEvent>(Assert.Single(restored.Events));
		Assert.Equal(EnemyId, @event.State.EntityId);
		Assert.Equal("crystal", @event.State.PrefabId);
		Assert.Equal(7f, @event.State.Health);
	}

	[Fact]
	public void CheckpointSplitAssemble_RoundTripsEnemies()
	{
		var kernel = new GameStateKernel(Epoch);
		Assert.True(Upsert(kernel, 1, new EnemyState(EnemyId, "spider", 9f, true, true)).IsAccepted);

		var restored = WireCheckpointAssembler.Assemble(WireCheckpointAssembler.Split(kernel.CreateCheckpoint()));

		var enemy = Assert.Single(restored.Enemies!.Enemies);
		Assert.Equal(EnemyId, enemy.EntityId);
		Assert.True(enemy.RuntimeSpawned);
	}

	[Fact]
	public void SaveLoad_RoundTripsEnemies()
	{
		var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cuo-enemies-{Guid.NewGuid():N}.bin");
		try
		{
			var authority = new ItemKernelAuthority(NullLogger<ItemKernelAuthority>.Instance);
			Assert.True(authority.TryUpsertEnemy(Host.Value, new EnemyState(EnemyId, "spider", 4f, false, false), out _, out _));

			var store = new KernelSaveFileStore(path, NullLogger<KernelSaveFileStore>.Instance);
			Assert.True(store.Save(authority.CreateCheckpoint()));
			Assert.True(store.TryLoad(out var loaded));

			var enemy = Assert.Single(loaded.Enemies!.Enemies);
			Assert.Equal(EnemyId, enemy.EntityId);
			Assert.Equal("spider", enemy.PrefabId);
		}
		finally
		{
			if (System.IO.File.Exists(path))
			{
				System.IO.File.Delete(path);
			}
		}
	}

	private static Decision Upsert(GameStateKernel kernel, ulong op, EnemyState state) =>
		kernel.Execute(
			new UpsertEnemyCommand(new OperationId(op), Host, Epoch, AuthorityKind.HostOnly, state),
			new CommandContext(Epoch, Host));
}
