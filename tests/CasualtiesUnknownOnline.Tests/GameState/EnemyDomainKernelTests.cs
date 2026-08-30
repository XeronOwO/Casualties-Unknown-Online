using System;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Entities;
using CasualtiesUnknownOnline.Protocol.Wire;
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

	[Fact]
	public void RecordEnemyBite_CommitsJournalEvent()
	{
		var kernel = new GameStateKernel(Epoch);

		var decision = kernel.Execute(
			new RecordEnemyBiteCommand(
				new OperationId(1),
				Host,
				Epoch,
				AuthorityKind.HostOnly,
				2001,
				Limb(),
				3f,
				75f,
				-0.5f),
			new CommandContext(Epoch, Host));

		Assert.True(decision.IsAccepted);
		var @event = Assert.IsType<EnemyBiteResultEvent>(Assert.Single(decision.Batch!.Events));
		Assert.Equal(2001UL, @event.VictimSteamId);
		Assert.Equal(2, @event.Limb.Index);
		Assert.Equal(3f, @event.VenomTotal);
	}

	[Fact]
	public void RecordEnemyLunge_CommitsJournalEvent()
	{
		var kernel = new GameStateKernel(Epoch);

		var decision = kernel.Execute(
			new RecordEnemyLungeCommand(
				new OperationId(1),
				Host,
				Epoch,
				AuthorityKind.HostOnly,
				2001,
				Limb(),
				70f,
				100f),
			new CommandContext(Epoch, Host));

		Assert.True(decision.IsAccepted);
		var @event = Assert.IsType<EnemyLungeResultEvent>(Assert.Single(decision.Batch!.Events));
		Assert.Equal(2001UL, @event.VictimSteamId);
		Assert.Equal(100f, @event.Stamina);
	}

	[Fact]
	public void RecordEnemyEffect_CommitsJournalEvent()
	{
		var kernel = new GameStateKernel(Epoch);

		var decision = kernel.Execute(
			new RecordEnemyEffectCommand(
				new OperationId(1),
				Host,
				Epoch,
				AuthorityKind.HostOnly,
				2001,
				EnemyCombatEffectKind.XalorisSepticTick,
				0f,
				0f,
				0f,
				0f,
				0f,
				0f,
				0f,
				12.074f,
				0f,
				0f),
			new CommandContext(Epoch, Host));

		Assert.True(decision.IsAccepted);
		var @event = Assert.IsType<EnemyEffectResultEvent>(Assert.Single(decision.Batch!.Events));
		Assert.Equal(2001UL, @event.VictimSteamId);
		Assert.Equal(EnemyCombatEffectKind.XalorisSepticTick, @event.Kind);
		Assert.Equal(12.074f, @event.SepticShock);
	}

	[Fact]
	public void WireBatchRoundTrip_PreservesEnemyBiteResultEvent()
	{
		var kernel = new GameStateKernel(Epoch);
		var decision = kernel.Execute(
			new RecordEnemyBiteCommand(new OperationId(1), Host, Epoch, AuthorityKind.HostOnly, 2001, Limb(), 3f, 75f, -0.5f),
			new CommandContext(Epoch, Host));

		var restored = KernelWireMapper.FromWireBatch(KernelWireMapper.ToWireBatch(decision.Batch!), Epoch);

		var @event = Assert.IsType<EnemyBiteResultEvent>(Assert.Single(restored.Events));
		Assert.Equal(2001UL, @event.VictimSteamId);
		Assert.Equal(2, @event.Limb.Index);
		Assert.Equal(75f, @event.Adrenaline);
	}

	[Fact]
	public void WireBatchRoundTrip_PreservesEnemyBiteLimbIndexZero()
	{
		var kernel = new GameStateKernel(Epoch);
		var decision = kernel.Execute(
			new RecordEnemyBiteCommand(
				new OperationId(1),
				Host,
				Epoch,
				AuthorityKind.HostOnly,
				2001,
				Limb() with { Index = 0 },
				3f,
				75f,
				-0.5f),
			new CommandContext(Epoch, Host));

		var restored = KernelWireMapper.FromWireBatch(KernelWireMapper.ToWireBatch(decision.Batch!), Epoch);

		var @event = Assert.IsType<EnemyBiteResultEvent>(Assert.Single(restored.Events));
		Assert.Equal(0, @event.Limb.Index);
	}

	[Fact]
	public void WireBatchRoundTrip_PreservesEnemyLungeResultEvent()
	{
		var kernel = new GameStateKernel(Epoch);
		var decision = kernel.Execute(
			new RecordEnemyLungeCommand(new OperationId(1), Host, Epoch, AuthorityKind.HostOnly, 2001, Limb(), 70f, 100f),
			new CommandContext(Epoch, Host));

		var restored = KernelWireMapper.FromWireBatch(KernelWireMapper.ToWireBatch(decision.Batch!), Epoch);

		var @event = Assert.IsType<EnemyLungeResultEvent>(Assert.Single(restored.Events));
		Assert.Equal(2001UL, @event.VictimSteamId);
		Assert.Equal(2, @event.Limb.Index);
		Assert.Equal(100f, @event.Stamina);
	}

	[Fact]
	public void WireBatchRoundTrip_PreservesEnemyEffectResultEvent()
	{
		var kernel = new GameStateKernel(Epoch);
		var decision = kernel.Execute(
			new RecordEnemyEffectCommand(new OperationId(1), Host, Epoch, AuthorityKind.HostOnly, 2001, EnemyCombatEffectKind.GrabberGrabbed, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 20f, 0.5f),
			new CommandContext(Epoch, Host));

		var restored = KernelWireMapper.FromWireBatch(KernelWireMapper.ToWireBatch(decision.Batch!), Epoch);

		var @event = Assert.IsType<EnemyEffectResultEvent>(Assert.Single(restored.Events));
		Assert.Equal(2001UL, @event.VictimSteamId);
		Assert.Equal(EnemyCombatEffectKind.GrabberGrabbed, @event.Kind);
		Assert.Equal(20f, @event.Shock);
	}

	[Fact]
	public void WireCommandRoundTrip_BuildsRecordEnemyBiteCommand()
	{
		var header = new EnvelopeHeader
		{
			RunEpoch = Epoch.Value,
			SenderId = 2001,
			OperationId = 7,
		};
		var wire = new WireCommand
		{
			Kind = WireCommandKind.RecordEnemyBite,
			EnemyCombat = new WireEnemyCombat
			{
				VictimSteamId = 2001,
				Limb = new WirePlayerInteractionLimb { Index = 2, SkinHealth = 80f },
				VenomTotal = 3f,
				Adrenaline = 75f,
				Happiness = -0.5f,
			},
		};

		var command = Assert.IsType<RecordEnemyBiteCommand>(KernelWireMapper.FromWireCommand(wire, header));

		Assert.Equal(2001UL, command.VictimSteamId);
		Assert.Equal(2, command.Limb.Index);
		Assert.Equal(75f, command.Adrenaline);
	}

	private static EnemyCombatLimb Limb() => new()
	{
		Index = 2,
		SkinHealth = 80f,
		MuscleHealth = 90f,
		BleedAmount = 4f,
		Pain = 12f,
		Infected = true,
		InfectionAmount = 1f,
	};

	private static Decision Upsert(GameStateKernel kernel, ulong op, EnemyState state) =>
		kernel.Execute(
			new UpsertEnemyCommand(new OperationId(op), Host, Epoch, AuthorityKind.HostOnly, state),
			new CommandContext(Epoch, Host));
}
