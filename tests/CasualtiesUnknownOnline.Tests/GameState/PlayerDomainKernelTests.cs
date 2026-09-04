using System;
using System.Linq;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.GameState.Domains.Players;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using System.IO;

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
		var path = Path.Combine(Path.GetTempPath(), $"cuo-players-limbs-{Guid.NewGuid():N}.bin");
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
			if (File.Exists(path))
			{
				File.Delete(path);
			}
		}
	}

	[Fact]
	public void UpdateStatus_UpsertsBodyTerminalFacts()
	{
		var kernel = new GameStateKernel(Epoch);
		var body = new PlayerBodyTerminalState(
			Disfigured: true,
			EyeGone: true,
			BothEyesGone: false,
			HasPulmonaryEmbolism: true,
			TriedRollingLastStand: false,
			SuccesfullyRolledLastStand: true,
			UsedNeuralBooster: true,
			FibrillationForced: false,
			MindwipeScriptPresent: true,
			MindwipeScriptActive: true);

		Assert.True(Update(kernel, 1, new PlayerState(2001, true, true, Body: body)).IsAccepted);

		var player = Assert.Single(kernel.QueryPlayers()!.Players);
		Assert.Equal(body, player.Body);
	}

	[Fact]
	public void WireBatchRoundTrip_PreservesPlayerBodyTerminalFacts()
	{
		var source = new GameStateKernel(Epoch);
		var body = new PlayerBodyTerminalState(
			Disfigured: false,
			EyeGone: true,
			BothEyesGone: true,
			HasPulmonaryEmbolism: false,
			TriedRollingLastStand: true,
			SuccesfullyRolledLastStand: false,
			UsedNeuralBooster: true,
			FibrillationForced: true,
			MindwipeScriptPresent: false,
			MindwipeScriptActive: true);
		var batch = Update(source, 1, new PlayerState(2001, true, true, Body: body)).Batch!;

		var restored = KernelWireMapper.FromWireBatch(KernelWireMapper.ToWireBatch(batch), Epoch);

		var @event = Assert.IsType<PlayerStatusUpdatedEvent>(Assert.Single(restored.Events));
		Assert.Equal(body, @event.State.Body);
	}

	[Fact]
	public void CheckpointSplitAssemble_RoundTripsPlayerBodyTerminalFacts()
	{
		var kernel = new GameStateKernel(Epoch);
		var body = new PlayerBodyTerminalState(
			Disfigured: true,
			EyeGone: false,
			BothEyesGone: false,
			HasPulmonaryEmbolism: true,
			TriedRollingLastStand: true,
			SuccesfullyRolledLastStand: true,
			UsedNeuralBooster: false,
			FibrillationForced: true,
			MindwipeScriptPresent: true,
			MindwipeScriptActive: false);
		Assert.True(Update(kernel, 1, new PlayerState(2001, true, true, Body: body)).IsAccepted);

		var restored = WireCheckpointAssembler.Assemble(WireCheckpointAssembler.Split(kernel.CreateCheckpoint()));

		Assert.Equal(body, Assert.Single(restored.Players!.Players).Body);
	}

	[Fact]
	public void SaveLoad_RoundTripsPlayerBodyTerminalFacts()
	{
		var path = Path.Combine(Path.GetTempPath(), $"cuo-players-body-{Guid.NewGuid():N}.bin");
		try
		{
			var authority = new ItemKernelAuthority(NullLogger<ItemKernelAuthority>.Instance);
			var body = new PlayerBodyTerminalState(
				Disfigured: false,
				EyeGone: true,
				BothEyesGone: false,
				HasPulmonaryEmbolism: false,
				TriedRollingLastStand: false,
				SuccesfullyRolledLastStand: false,
				UsedNeuralBooster: true,
				FibrillationForced: false,
				MindwipeScriptPresent: true,
				MindwipeScriptActive: true);
			Assert.True(authority.TryUpdatePlayerStatus(
				Host.Value,
				new PlayerState(2001, true, true, Body: body),
				out _,
				out _));

			var store = new KernelSaveFileStore(path, NullLogger<KernelSaveFileStore>.Instance);
			Assert.True(store.Save(authority.CreateCheckpoint()));
			Assert.True(store.TryLoad(out var loaded));

			Assert.Equal(body, Assert.Single(loaded.Players!.Players).Body);
		}
		finally
		{
			if (File.Exists(path))
			{
				File.Delete(path);
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
	public void DeadCarrier_IsRejectedByInvariant()
	{
		var kernel = new GameStateKernel(Epoch);
		Assert.True(Update(kernel, 1, new PlayerState(2001, false, false)).IsAccepted);
		Assert.True(Update(kernel, 2, new PlayerState(2002, false, false)).IsAccepted);

		var decision = SetCarry(kernel, 3, 2001, 2002);

		Assert.False(decision.IsAccepted);
		Assert.Equal(RejectionReason.InvariantViolation, decision.Rejection!.Reason);
	}

	[Fact]
	public void UnconsciousButAliveCarrier_IsRejectedByInvariant()
	{
		var kernel = new GameStateKernel(Epoch);
		Assert.True(Update(kernel, 1, new PlayerState(2001, true, false)).IsAccepted);
		Assert.True(Update(kernel, 2, new PlayerState(2002, false, false)).IsAccepted);

		var decision = SetCarry(kernel, 3, 2001, 2002);

		Assert.False(decision.IsAccepted);
		Assert.Equal(RejectionReason.InvariantViolation, decision.Rejection!.Reason);
	}

	[Fact]
	public void CarriedItemWithUnknownOwner_IsRejectedByPlayerInvariant()
	{
		var kernel = new GameStateKernel(Epoch);
		Assert.True(Update(kernel, 1, new PlayerState(2001, true, true)).IsAccepted);

		var spawn = kernel.Execute(
			new SpawnItemCommand(
				new OperationId(2),
				Host,
				Epoch,
				AuthorityKind.OwnerPredictedHostValidated,
				new ItemIdentity(42, "medkit"),
				ItemLocation.Carried(new ActorId(9999)),
				0,
				null),
			new CommandContext(Epoch, Host));
		Assert.True(spawn.IsAccepted);

		var decision = Update(kernel, 3, new PlayerState(2002, true, true));

		Assert.False(decision.IsAccepted);
		Assert.Equal(RejectionReason.InvariantViolation, decision.Rejection!.Reason);
	}

	[Fact]
	public void DeadStatusUpdate_PreservesCarriedItems()
	{
		var kernel = new GameStateKernel(Epoch);
		Assert.True(Update(kernel, 1, new PlayerState(2001, true, true)).IsAccepted);

		var spawn = kernel.Execute(
			new SpawnItemCommand(
				new OperationId(2),
				Host,
				Epoch,
				AuthorityKind.OwnerPredictedHostValidated,
				new ItemIdentity(42, "medkit"),
				ItemLocation.Carried(new ActorId(2001)),
				0,
				null),
			new CommandContext(Epoch, Host));
		Assert.True(spawn.IsAccepted);

		Assert.True(Update(kernel, 3, new PlayerState(2001, false, false)).IsAccepted);

		var item = kernel.FindItem(42);
		Assert.NotNull(item);
		Assert.Equal(ItemLocationKind.Carried, item!.Value.Location.Kind);
		Assert.Equal(2001ul, item.Value.Location.Owner.Value);
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
		var path = Path.Combine(Path.GetTempPath(), $"cuo-players-{Guid.NewGuid():N}.bin");
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
			if (File.Exists(path))
			{
				File.Delete(path);
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
		var path = Path.Combine(Path.GetTempPath(), $"cuo-players-carry-{Guid.NewGuid():N}.bin");
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
			if (File.Exists(path))
			{
				File.Delete(path);
			}
		}
	}

	[Fact]
	public void RecordPlayerInventoryTransfer_CommitsJournalEvent()
	{
		var kernel = new GameStateKernel(Epoch);
		var item = new PlayerInteractionItem(new ItemIdentity(42, "medkit"), ItemData.Empty);

		var decision = kernel.Execute(
			new RecordPlayerInventoryTransferCommand(new OperationId(1), Host, Epoch, AuthorityKind.HostOnly, 2001, 2002, item),
			new CommandContext(Epoch, Host));

		Assert.True(decision.IsAccepted);
		var @event = Assert.IsType<PlayerInventoryTransferEvent>(Assert.Single(decision.Batch!.Events));
		Assert.Equal(2001ul, @event.FromSteamId);
		Assert.Equal(2002ul, @event.ToSteamId);
		Assert.Equal(42ul, @event.Item.Identity.InstanceId);
	}

	[Fact]
	public void WireBatchRoundTrip_PreservesPlayerInventoryTransferEvent()
	{
		var source = new GameStateKernel(Epoch);
		var child = new PlayerInteractionItem(new ItemIdentity(43, "bandage"), ItemData.Empty);
		var item = new PlayerInteractionItem(new ItemIdentity(42, "backpack"), new ItemData(0.75f, true, 0, [], []), [child]);
		var batch = source.Execute(
			new RecordPlayerInventoryTransferCommand(new OperationId(1), Host, Epoch, AuthorityKind.HostOnly, 2001, 2002, item),
			new CommandContext(Epoch, Host)).Batch!;

		var restored = KernelWireMapper.FromWireBatch(KernelWireMapper.ToWireBatch(batch), Epoch);

		var @event = Assert.IsType<PlayerInventoryTransferEvent>(Assert.Single(restored.Events));
		Assert.Equal(2001ul, @event.FromSteamId);
		Assert.Equal(2002ul, @event.ToSteamId);
		Assert.Equal("backpack", @event.Item.Identity.DefinitionId);
		Assert.Equal(0.75f, @event.Item.Data.Condition);
		Assert.True(@event.Item.Data.Favourited);
		var restoredChild = Assert.Single(@event.Item.Children);
		Assert.Equal(43ul, restoredChild.Identity.InstanceId);
		Assert.Equal("bandage", restoredChild.Identity.DefinitionId);
	}

	[Fact]
	public void WireBatchRoundTrip_PreservesContainerMoveParentOnTransferEvent()
	{
		var source = new GameStateKernel(Epoch);
		var item = new PlayerInteractionItem(new ItemIdentity(42, "waterbottle"), ItemData.Empty);
		var batch = source.Execute(
			new RecordPlayerInventoryTransferCommand(new OperationId(1), Host, Epoch, AuthorityKind.HostOnly, 2001, 2001, item, 500UL),
			new CommandContext(Epoch, Host)).Batch!;

		var restored = KernelWireMapper.FromWireBatch(KernelWireMapper.ToWireBatch(batch), Epoch);

		var @event = Assert.IsType<PlayerInventoryTransferEvent>(Assert.Single(restored.Events));
		Assert.Equal(500UL, @event.TargetParentItemId);
	}

	[Fact]
	public void RecordPlayerHealResult_CommitsJournalEvent()
	{
		var kernel = new GameStateKernel(Epoch);
		var health = new PlayerInteractionHealth { OpiateAmount = 28f, BrainHealth = 70f };
		var limb = new PlayerInteractionLimb { Index = 1, SkinHealth = 80f };

		var decision = kernel.Execute(
			new RecordPlayerHealResultCommand(
				new OperationId(1),
				Host,
				Epoch,
				AuthorityKind.HostOnly,
				2001,
				2002,
				42,
				true,
				0f,
				1,
				health,
				[limb]),
			new CommandContext(Epoch, Host));

		Assert.True(decision.IsAccepted);
		var @event = Assert.IsType<PlayerHealResultEvent>(Assert.Single(decision.Batch!.Events));
		Assert.Equal(2001ul, @event.HealerSteamId);
		Assert.Equal(2002ul, @event.TargetSteamId);
		Assert.True(@event.ItemDestroyed);
		Assert.Equal(28f, @event.Health!.OpiateAmount);
		Assert.Equal(1, Assert.Single(@event.Limbs).Index);
	}

	[Fact]
	public void WireBatchRoundTrip_PreservesPlayerHealResultEvent()
	{
		var source = new GameStateKernel(Epoch);
		var health = new PlayerInteractionHealth { BloodVolume = 100f, OpiateAmount = 28f };
		var limb = new PlayerInteractionLimb { Index = 1, SkinHealth = 80f, Broken = true };
		var batch = source.Execute(
			new RecordPlayerHealResultCommand(
				new OperationId(1),
				Host,
				Epoch,
				AuthorityKind.HostOnly,
				2001,
				2002,
				42,
				false,
				0.5f,
				1,
				health,
				[limb]),
			new CommandContext(Epoch, Host)).Batch!;

		var restored = KernelWireMapper.FromWireBatch(KernelWireMapper.ToWireBatch(batch), Epoch);

		var @event = Assert.IsType<PlayerHealResultEvent>(Assert.Single(restored.Events));
		Assert.Equal(2001ul, @event.HealerSteamId);
		Assert.Equal(2002ul, @event.TargetSteamId);
		Assert.False(@event.ItemDestroyed);
		Assert.Equal(0.5f, @event.ItemConditionAfter);
		Assert.Equal(28f, @event.Health!.OpiateAmount);
		var restoredLimb = Assert.Single(@event.Limbs);
		Assert.Equal(1, restoredLimb.Index);
		Assert.True(restoredLimb.Broken);
	}

	[Fact]
	public void WireBatchRoundTrip_PreservesPlayerItemUseResultEvent()
	{
		var source = new GameStateKernel(Epoch);
		var after = new PlayerInteractionItem(new ItemIdentity(42, "waterbottle"), new ItemData(0.8f, false, 0, [], []));
		var worn = new PlayerInteractionItem(new ItemIdentity(7, "bikehelmet"), new ItemData(1f, false, -2, [], []));
		var timedBody = new PlayerInteractionTimedBodyEffect("highgradestimulant", 144f, 60f);
		var batch = source.Execute(
			new RecordPlayerItemUseResultCommand(
				new OperationId(1),
				Host,
				Epoch,
				AuthorityKind.HostOnly,
				2001,
				2002,
				42,
				false,
				after,
				worn,
				new PlayerInteractionHealth { Thirst = 9f },
				[new PlayerInteractionLimb { Index = 1 }],
				[new PlayerInteractionTimedLimbEffect(1, 10f, -4.5f)],
				[timedBody]),
			new CommandContext(Epoch, Host)).Batch!;

		var restored = KernelWireMapper.FromWireBatch(KernelWireMapper.ToWireBatch(batch), Epoch);

		var @event = Assert.IsType<PlayerItemUseResultEvent>(Assert.Single(restored.Events));
		Assert.Equal(2001ul, @event.UserSteamId);
		Assert.Equal(2002ul, @event.TargetSteamId);
		Assert.Equal("waterbottle", @event.ItemAfter!.Identity.DefinitionId);
		Assert.Equal(0.8f, @event.ItemAfter.Data.Condition);
		Assert.Equal("bikehelmet", @event.WornItem!.Identity.DefinitionId);
		Assert.Equal(9f, @event.Health!.Thirst);
		Assert.Equal(1, Assert.Single(@event.Limbs).Index);
		Assert.Equal(10f, Assert.Single(@event.TimedEffects).DurationSeconds);
		Assert.Equal("highgradestimulant", Assert.Single(@event.TimedBodyEffects).EffectId);
	}

	[Fact]
	public void UpdateStatus_UpsertsSkills()
	{
		var kernel = new GameStateKernel(Epoch);
		var skills = new PlayerSkillsState(15, 12, 9, 3.5f, 2.25f, 1.75f);

		Assert.True(Update(kernel, 1, new PlayerState(2001, true, true, Skills: skills)).IsAccepted);

		Assert.Equal(skills, Assert.Single(kernel.QueryPlayers()!.Players).Skills);
	}

	[Fact]
	public void WireBatchRoundTrip_PreservesPlayerSkills()
	{
		var source = new GameStateKernel(Epoch);
		var skills = new PlayerSkillsState(15, 12, 9, 3.5f, 2.25f, 1.75f);
		var batch = Update(source, 1, new PlayerState(2001, true, true, Skills: skills)).Batch!;

		var restored = KernelWireMapper.FromWireBatch(KernelWireMapper.ToWireBatch(batch), Epoch);

		var @event = Assert.IsType<PlayerStatusUpdatedEvent>(Assert.Single(restored.Events));
		Assert.Equal(skills, @event.State.Skills);
	}

	[Fact]
	public void CheckpointSplitAssemble_RoundTripsPlayerSkills()
	{
		var kernel = new GameStateKernel(Epoch);
		var skills = new PlayerSkillsState(15, 12, 9, 3.5f, 2.25f, 1.75f);
		Assert.True(Update(kernel, 1, new PlayerState(2001, true, true, Skills: skills)).IsAccepted);

		var restored = WireCheckpointAssembler.Assemble(WireCheckpointAssembler.Split(kernel.CreateCheckpoint()));

		Assert.Equal(skills, Assert.Single(restored.Players!.Players).Skills);
	}

	[Fact]
	public void SaveLoad_RoundTripsPlayerSkills()
	{
		var path = Path.Combine(Path.GetTempPath(), $"cuo-players-skills-{Guid.NewGuid():N}.bin");
		try
		{
			var authority = new ItemKernelAuthority(NullLogger<ItemKernelAuthority>.Instance);
			var skills = new PlayerSkillsState(15, 12, 9, 3.5f, 2.25f, 1.75f);
			Assert.True(authority.TryUpdatePlayerStatus(
				Host.Value,
				new PlayerState(2001, true, true, Skills: skills),
				out _,
				out _));

			var store = new KernelSaveFileStore(path, NullLogger<KernelSaveFileStore>.Instance);
			Assert.True(store.Save(authority.CreateCheckpoint()));
			Assert.True(store.TryLoad(out var loaded));

			Assert.Equal(skills, Assert.Single(loaded.Players!.Players).Skills);
		}
		finally
		{
			if (File.Exists(path))
			{
				File.Delete(path);
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
