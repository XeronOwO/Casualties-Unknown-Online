using System;
using System.Linq;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Entities;
using CasualtiesUnknownOnline.GameState.Domains.Fluids;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.GameState.Domains.Players;
using CasualtiesUnknownOnline.GameState.Domains.World;
using CasualtiesUnknownOnline.GameState.Domains.WorldEntities;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// Maps between the deterministic kernel model and the Phase C wire DTOs.
/// This is the only Runtime surface allowed to touch both GameState and
/// Protocol. The mapper is pure and has no network/session state.
/// </summary>
public static class KernelWireMapper
{
	// ===== Kernel -> Wire =====

	public static WireRandomStream ToWireRandomStream(RandomStreamState state) =>
		new()
		{
			Name = state.Name,
			State = state.State,
			DecidedValues = [.. state.DecidedValues],
		};

	public static RandomStreamState FromWireRandomStream(WireRandomStream stream) =>
		new(stream.Name, stream.State, [.. stream.DecidedValues]);

	public static WireItem ToWireItem(ItemState state) =>
		new()
		{
			Identity = ToWireIdentity(state.Identity),
			Revision = state.Revision,
			Location = ToWireLocation(state.Location),
			Data = ToWireData(state.Data),
		};

	public static WireItemIdentity ToWireIdentity(ItemIdentity identity) =>
		new()
		{
			InstanceId = identity.InstanceId,
			DefinitionId = identity.DefinitionId,
		};

	public static WireItemLocation ToWireLocation(ItemLocation location) =>
		new()
		{
			Kind = location.Kind switch
			{
				ItemLocationKind.World => WireItemLocationKind.World,
				ItemLocationKind.Carried => WireItemLocationKind.Carried,
				ItemLocationKind.Contained => WireItemLocationKind.Contained,
				ItemLocationKind.Terminal => WireItemLocationKind.Terminal,
				_ => throw new ArgumentOutOfRangeException(nameof(location), location.Kind, "unknown item location kind"),
			},
			Owner = location.Owner.Value,
			ParentItemId = location.ParentItemId,
			X = location.X,
			Y = location.Y,
		};

	public static WireItemData ToWireData(ItemData data) =>
		new()
		{
			Condition = data.Condition,
			Favourited = data.Favourited,
			SlotIndex = data.SlotIndex,
			Liquids = [.. data.Liquids.Select(l => new WireLiquidStack { LiquidId = l.LiquidId, Amount = l.Amount })],
			Components = [.. data.Components.Select(ToWireComponent)],
		};

	private static WireComponentState ToWireComponent(ItemComponentState component) =>
		new()
		{
			TypeName = component.TypeName,
			Fields = [.. component.Fields.Select(ToWireField)],
		};

	private static WireComponentField ToWireField(ItemComponentField field) =>
		new()
		{
			Name = field.Name,
			Kind = (int)field.Kind,
			FloatValue = field.FloatValue,
			IntValue = field.IntValue,
			BoolValue = field.BoolValue,
			StringValue = field.StringValue,
			StringList = [.. field.StringList],
		};

	public static WireCommittedBatch ToWireBatch(CommittedBatch batch) =>
		new()
		{
			OperationId = batch.OperationId.Value,
			GlobalRevision = batch.GlobalRevision,
			Actor = batch.Actor.Value,
			Authority = (int)batch.Authority,
			RunEpoch = batch.RunEpoch.Value,
			Preconditions = [.. batch.Preconditions.Select(p => new WireExpectedRevision
			{
				AggregateId = p.AggregateId,
				Revision = p.Revision,
			})],
			Events = [.. batch.Events.Select(ToWireEvent)],
		};

	public static WireEvent ToWireEvent(GameEvent @event) =>
		@event switch
		{
			ItemSpawnedEvent spawned => new WireEvent
			{
				Kind = WireEventKind.ItemSpawned,
				Identity = ToWireIdentity(spawned.Identity),
				NewRevision = spawned.Revision,
				NewLocation = ToWireLocation(spawned.Location),
				NewData = spawned.Data is null ? null : ToWireData(spawned.Data.Value),
			},
			ItemRelocatedEvent relocated => new WireEvent
			{
				Kind = WireEventKind.ItemRelocated,
				Identity = ToWireIdentity(relocated.Identity),
				OldRevision = relocated.OldRevision,
				NewRevision = relocated.NewRevision,
				OldLocation = ToWireLocation(relocated.OldLocation),
				NewLocation = ToWireLocation(relocated.NewLocation),
				NewData = relocated.NewData is null ? null : ToWireData(relocated.NewData.Value),
			},
			ItemDestroyedEvent destroyed => new WireEvent
			{
				Kind = WireEventKind.ItemDestroyed,
				Identity = ToWireIdentity(destroyed.Identity),
				NewRevision = destroyed.Revision,
				NewLocation = ToWireLocation(destroyed.TerminalLocation),
				TerminalKind = destroyed.Kind switch
				{
					TerminalKind.Consumed => WireTerminalKind.Consumed,
					TerminalKind.Destroyed => WireTerminalKind.Destroyed,
					TerminalKind.ReplacedBy => WireTerminalKind.ReplacedBy,
					_ => throw new ArgumentOutOfRangeException(nameof(destroyed.Kind), destroyed.Kind, "unknown terminal kind"),
				},
			},
			ItemDataUpdatedEvent updated => new WireEvent
			{
				Kind = WireEventKind.ItemDataUpdated,
				Identity = ToWireIdentity(updated.Identity),
				OldRevision = updated.OldRevision,
				NewRevision = updated.NewRevision,
				OldData = ToWireData(updated.OldData),
				NewData = ToWireData(updated.NewData),
			},
			RunStartedEvent started => new WireEvent
			{
				Kind = WireEventKind.RunStarted,
				RunState = KernelDomainWireMapper.ToWireRun(started.Run),
			},
			RunAdvancedEvent advanced => new WireEvent
			{
				Kind = WireEventKind.RunAdvanced,
				RunState = KernelDomainWireMapper.ToWireRun(advanced.Run),
			},
			TrapConsumedEvent trap => new WireEvent
			{
				Kind = WireEventKind.TrapConsumed,
				EntityPosition = KernelDomainWireMapper.ToWireEntityPosition(trap.Position),
				EntityKind = trap.Kind,
				Extra = trap.Extra,
				TriggeredAtMs = trap.TriggeredAtMs,
			},
			BuildingEntityHealthUpdatedEvent health => new WireEvent
			{
				Kind = WireEventKind.BuildingEntityHealthUpdated,
				EntityPosition = KernelDomainWireMapper.ToWireEntityPosition(health.Position),
				Health = health.Health,
			},
			OpenedEntityEvent opened => new WireEvent
			{
				Kind = WireEventKind.OpenedEntity,
				EntityPosition = KernelDomainWireMapper.ToWireEntityPosition(opened.Position),
			},
			WorldEntitiesResetEvent => new WireEvent
			{
				Kind = WireEventKind.WorldEntitiesReset,
			},
			PlayerStatusUpdatedEvent updated => new WireEvent
			{
				Kind = WireEventKind.PlayerStatusUpdated,
				PlayerState = KernelDomainWireMapper.ToWirePlayerState(updated.State),
			},
			PlayersResetEvent => new WireEvent
			{
				Kind = WireEventKind.PlayersReset,
			},
			PlayerCarrySetEvent carrySet => new WireEvent
			{
				Kind = WireEventKind.PlayerCarrySet,
				CarrierSteamId = carrySet.CarrierSteamId,
				CarriedSteamId = carrySet.CarriedSteamId,
			},
			PlayerCarryClearedEvent carryClear => new WireEvent
			{
				Kind = WireEventKind.PlayerCarryCleared,
				CarrierSteamId = carryClear.CarrierSteamId,
				CarriedSteamId = carryClear.CarriedSteamId,
			},
			PlayerInventoryTransferEvent transfer => new WireEvent
			{
				Kind = WireEventKind.PlayerInventoryTransfer,
				PlayerInteraction = PlayerInteractionWireMapper.ToWire(transfer),
			},
			PlayerHealResultEvent heal => new WireEvent
			{
				Kind = WireEventKind.PlayerHealResult,
				PlayerInteraction = PlayerInteractionWireMapper.ToWire(heal),
			},
			PlayerItemUseResultEvent use => new WireEvent
			{
				Kind = WireEventKind.PlayerItemUseResult,
				PlayerInteraction = PlayerInteractionWireMapper.ToWire(use),
			},
			EnemyBiteResultEvent bite => new WireEvent
			{
				Kind = WireEventKind.EnemyBiteResult,
				EnemyCombat = EnemyCombatWireMapper.ToWire(bite),
			},
			EnemyLungeResultEvent lunge => new WireEvent
			{
				Kind = WireEventKind.EnemyLungeResult,
				EnemyCombat = EnemyCombatWireMapper.ToWire(lunge),
			},
			EnemyEffectResultEvent effect => new WireEvent
			{
				Kind = WireEventKind.EnemyEffectResult,
				EnemyCombat = EnemyCombatWireMapper.ToWire(effect),
			},
			EnemyUpsertedEvent upserted => new WireEvent
			{
				Kind = WireEventKind.EnemyUpserted,
				EnemyState = KernelDomainWireMapper.ToWireEnemyState(upserted.State),
			},
			EnemyRemovedEvent removed => new WireEvent
			{
				Kind = WireEventKind.EnemyRemoved,
				EntityId = KernelDomainWireMapper.ToWireEntityId(removed.EntityId),
			},
			EnemiesResetEvent => new WireEvent
			{
				Kind = WireEventKind.EnemiesReset,
			},
			FluidRegionUpdatedEvent fluid => new WireEvent
			{
				Kind = WireEventKind.FluidRegionUpdated,
				FluidState = KernelDomainWireMapper.ToWireFluidRegionState(fluid.State),
			},
			FluidsResetEvent => new WireEvent
			{
				Kind = WireEventKind.FluidsReset,
			},
			_ => throw new ArgumentOutOfRangeException(nameof(@event), @event.GetType().Name, "no wire mapping for kernel event"),
		};

	// ===== Wire -> Kernel =====

	public static ItemState FromWireItem(WireItem item) =>
		new(
			FromWireIdentity(item.Identity),
			item.Revision,
			FromWireLocation(item.Location))
		{
			Data = FromWireData(item.Data),
		};

	public static ItemIdentity FromWireIdentity(WireItemIdentity identity) =>
		new(identity.InstanceId, identity.DefinitionId);

	public static ItemLocation FromWireLocation(WireItemLocation location) =>
		location.Kind switch
		{
			WireItemLocationKind.World => ItemLocation.World(location.X, location.Y, location.ParentItemId),
			WireItemLocationKind.Carried => ItemLocation.Carried(new ActorId(location.Owner)),
			WireItemLocationKind.Contained => ItemLocation.Contained(new ActorId(location.Owner), location.ParentItemId),
			WireItemLocationKind.Terminal => ItemLocation.Terminal(),
			_ => throw new ArgumentOutOfRangeException(nameof(location), location.Kind, "unknown wire item location kind"),
		};

	public static ItemData FromWireData(WireItemData data) =>
		new(
			data.Condition,
			data.Favourited,
			data.SlotIndex,
			[.. data.Liquids.Select(l => new ItemLiquidStack(l.LiquidId, l.Amount))],
			[.. data.Components.Select(c => new ItemComponentState(
				c.TypeName,
				[.. c.Fields.Select(f => new ItemComponentField(
					f.Name,
					(ItemComponentFieldKind)f.Kind,
					f.FloatValue,
					f.IntValue,
					f.BoolValue,
					f.StringValue,
					f.StringList))]))]);

	public static CommittedBatch FromWireBatch(WireCommittedBatch batch, RunEpoch fallbackEpoch) =>
		new(
			new OperationId(batch.OperationId),
			batch.GlobalRevision,
			new ActorId(batch.Actor),
			(AuthorityKind)batch.Authority,
			batch.RunEpoch == 0 ? fallbackEpoch : new RunEpoch(batch.RunEpoch),
			[.. batch.Preconditions.Select(p => new ExpectedRevision(p.AggregateId, p.Revision))],
			[.. batch.Events.Select(FromWireEvent)]);

	public static GameEvent FromWireEvent(WireEvent @event) =>
		@event.Kind switch
		{
			WireEventKind.ItemSpawned => new ItemSpawnedEvent(
				FromWireIdentity(@event.Identity),
				@event.NewRevision,
				FromWireLocation(@event.NewLocation ?? new WireItemLocation { Kind = WireItemLocationKind.Terminal }),
				@event.NewData is null ? null : FromWireData(@event.NewData)),
			WireEventKind.ItemRelocated => new ItemRelocatedEvent(
				FromWireIdentity(@event.Identity),
				@event.OldRevision,
				@event.NewRevision,
				FromWireLocation(@event.OldLocation ?? new WireItemLocation { Kind = WireItemLocationKind.Terminal }),
				FromWireLocation(@event.NewLocation ?? new WireItemLocation { Kind = WireItemLocationKind.Terminal }),
				@event.NewData is null ? null : FromWireData(@event.NewData)),
			WireEventKind.ItemDestroyed => new ItemDestroyedEvent(
				FromWireIdentity(@event.Identity),
				@event.NewRevision,
				FromWireLocation(@event.NewLocation ?? new WireItemLocation { Kind = WireItemLocationKind.Terminal }),
				@event.TerminalKind switch
				{
					WireTerminalKind.Consumed => TerminalKind.Consumed,
					WireTerminalKind.Destroyed => TerminalKind.Destroyed,
					WireTerminalKind.ReplacedBy => TerminalKind.ReplacedBy,
					_ => TerminalKind.Destroyed,
				}),
			WireEventKind.ItemDataUpdated => new ItemDataUpdatedEvent(
				FromWireIdentity(@event.Identity),
				@event.OldRevision,
				@event.NewRevision,
				@event.OldData is null ? ItemData.Empty : FromWireData(@event.OldData),
				@event.NewData is null ? ItemData.Empty : FromWireData(@event.NewData)),
			WireEventKind.RunStarted => new RunStartedEvent(
				KernelDomainWireMapper.FromWireRun(@event.RunState ?? throw new InvalidOperationException("RunStarted event lacks run state"))),
			WireEventKind.RunAdvanced => new RunAdvancedEvent(
				KernelDomainWireMapper.FromWireRun(@event.RunState ?? throw new InvalidOperationException("RunAdvanced event lacks run state"))),
			WireEventKind.TrapConsumed => new TrapConsumedEvent(
				KernelDomainWireMapper.FromWireEntityPosition(@event.EntityPosition ?? throw new InvalidOperationException("TrapConsumed event lacks position")),
				@event.EntityKind,
				@event.Extra,
				@event.TriggeredAtMs),
			WireEventKind.BuildingEntityHealthUpdated => new BuildingEntityHealthUpdatedEvent(
				KernelDomainWireMapper.FromWireEntityPosition(@event.EntityPosition ?? throw new InvalidOperationException("BuildingEntityHealthUpdated event lacks position")),
				@event.Health),
			WireEventKind.OpenedEntity => new OpenedEntityEvent(
				KernelDomainWireMapper.FromWireEntityPosition(@event.EntityPosition ?? throw new InvalidOperationException("OpenedEntity event lacks position"))),
			WireEventKind.WorldEntitiesReset => new WorldEntitiesResetEvent(),
			WireEventKind.PlayerStatusUpdated => new PlayerStatusUpdatedEvent(
				KernelDomainWireMapper.FromWirePlayerState(@event.PlayerState ?? throw new InvalidOperationException("PlayerStatusUpdated event lacks player state"))),
			WireEventKind.PlayersReset => new PlayersResetEvent(),
			WireEventKind.PlayerCarrySet => new PlayerCarrySetEvent(
				@event.CarrierSteamId,
				@event.CarriedSteamId),
			WireEventKind.PlayerCarryCleared => new PlayerCarryClearedEvent(
				@event.CarrierSteamId,
				@event.CarriedSteamId),
			WireEventKind.PlayerInventoryTransfer => PlayerInteractionWireMapper.FromWireInventoryTransfer(
				@event.PlayerInteraction ?? throw new InvalidOperationException("PlayerInventoryTransfer event lacks interaction payload")),
			WireEventKind.PlayerHealResult => PlayerInteractionWireMapper.FromWireHealResult(
				@event.PlayerInteraction ?? throw new InvalidOperationException("PlayerHealResult event lacks interaction payload")),
			WireEventKind.PlayerItemUseResult => PlayerInteractionWireMapper.FromWireItemUseResult(
				@event.PlayerInteraction ?? throw new InvalidOperationException("PlayerItemUseResult event lacks interaction payload")),
			WireEventKind.EnemyBiteResult => EnemyCombatWireMapper.FromWireBiteResult(
				@event.EnemyCombat ?? throw new InvalidOperationException("EnemyBiteResult event lacks enemy combat payload")),
			WireEventKind.EnemyLungeResult => EnemyCombatWireMapper.FromWireLungeResult(
				@event.EnemyCombat ?? throw new InvalidOperationException("EnemyLungeResult event lacks enemy combat payload")),
			WireEventKind.EnemyEffectResult => EnemyCombatWireMapper.FromWireEffectResult(
				@event.EnemyCombat ?? throw new InvalidOperationException("EnemyEffectResult event lacks enemy combat payload")),
			WireEventKind.EnemyUpserted => new EnemyUpsertedEvent(
				KernelDomainWireMapper.FromWireEnemyState(@event.EnemyState ?? throw new InvalidOperationException("EnemyUpserted event lacks enemy state"))),
			WireEventKind.EnemyRemoved => new EnemyRemovedEvent(
				KernelDomainWireMapper.FromWireEntityId(@event.EntityId ?? throw new InvalidOperationException("EnemyRemoved event lacks entity id"))),
			WireEventKind.EnemiesReset => new EnemiesResetEvent(),
			WireEventKind.FluidRegionUpdated => new FluidRegionUpdatedEvent(
				KernelDomainWireMapper.FromWireFluidRegionState(@event.FluidState ?? throw new InvalidOperationException("FluidRegionUpdated event lacks fluid state"))),
			WireEventKind.FluidsReset => new FluidsResetEvent(),
			_ => throw new ArgumentOutOfRangeException(nameof(@event.Kind), @event.Kind, "unknown wire event kind"),
		};

	public static GameCommand FromWireCommand(WireCommand command, EnvelopeHeader header)
	{
		var operation = new OperationId(header.OperationId);
		var actor = new ActorId(header.SenderId);
		var epoch = new RunEpoch(header.RunEpoch);
		var authority = AuthorityKind.OwnerPredictedHostValidated;
		var identity = FromWireIdentity(command.Identity);

		return command.Kind switch
		{
			WireCommandKind.ItemSpawn => new SpawnItemCommand(
				operation,
				actor,
				epoch,
				authority,
				identity,
				FromWireLocation(command.Location ?? throw new InvalidOperationException("spawn command lacks location")),
				0,
				command.Data is null ? null : FromWireData(command.Data)),
			WireCommandKind.ItemPickup => new PickUpItemCommand(
				operation,
				actor,
				epoch,
				authority,
				identity.InstanceId,
				new ActorId(command.NewOwner),
				command.ExpectedRevision),
			WireCommandKind.ItemDrop => new DropItemCommand(
				operation,
				actor,
				epoch,
				authority,
				identity.InstanceId,
				FromWireLocation(command.Location ?? throw new InvalidOperationException("drop command lacks location")),
				command.ExpectedRevision,
				command.Data is null ? null : FromWireData(command.Data)),
			WireCommandKind.ItemDestroy => new DestroyItemCommand(
				operation,
				actor,
				epoch,
				authority,
				identity.InstanceId,
				command.TerminalKind switch
				{
					WireTerminalKind.Consumed => TerminalKind.Consumed,
					WireTerminalKind.Destroyed => TerminalKind.Destroyed,
					WireTerminalKind.ReplacedBy => TerminalKind.ReplacedBy,
					_ => TerminalKind.Destroyed,
				},
				command.ExpectedRevision),
			WireCommandKind.ItemUpdateState => new UpdateItemStateCommand(
				operation,
				actor,
				epoch,
				authority,
				identity.InstanceId,
				command.Data is null ? ItemData.Empty : FromWireData(command.Data),
				command.ExpectedRevision),
			WireCommandKind.ItemTransfer => new TransferItemCommand(
				operation,
				actor,
				epoch,
				authority,
				identity.InstanceId,
				new ActorId(command.NewOwner),
				command.Data is null ? null : FromWireData(command.Data),
				command.ExpectedRevision),
			WireCommandKind.ItemContainerSync => new SyncContainerItemsCommand(
				operation,
				actor,
				epoch,
				authority,
				identity,
				command.Data is null ? ItemData.Empty : FromWireData(command.Data),
				[.. command.ContainerChildren.Select(c => new ContainerChildFact(
					c.Identity.InstanceId,
					c.Identity.DefinitionId,
					c.ParentItemId,
					FromWireData(c.Data)))]),
			WireCommandKind.RunStart => new StartRunCommand(
				operation,
				actor,
				epoch,
				authority,
				KernelDomainWireMapper.FromWireRun(command.RunState ?? throw new InvalidOperationException("RunStart command lacks run state"))),
			WireCommandKind.AdvanceLayer => new AdvanceLayerCommand(
				operation,
				actor,
				epoch,
				authority,
				KernelDomainWireMapper.FromWireRun(command.RunState ?? throw new InvalidOperationException("AdvanceLayer command lacks run state"))),
			WireCommandKind.RecordTrapConsumed => new RecordTrapConsumedCommand(
				operation,
				actor,
				epoch,
				authority,
				KernelDomainWireMapper.FromWireEntityPosition(command.EntityPosition ?? throw new InvalidOperationException("RecordTrapConsumed command lacks position")),
				command.EntityKind,
				command.Extra,
				command.TriggeredAtMs),
			WireCommandKind.RecordBuildingEntityHealth => new RecordBuildingEntityHealthCommand(
				operation,
				actor,
				epoch,
				authority,
				KernelDomainWireMapper.FromWireEntityPosition(command.EntityPosition ?? throw new InvalidOperationException("RecordBuildingEntityHealth command lacks position")),
				command.Health),
			WireCommandKind.RecordOpenedEntity => new RecordOpenedEntityCommand(
				operation,
				actor,
				epoch,
				authority,
				KernelDomainWireMapper.FromWireEntityPosition(command.EntityPosition ?? throw new InvalidOperationException("RecordOpenedEntity command lacks position"))),
			WireCommandKind.ResetWorldEntities => new ResetWorldEntitiesCommand(
				operation,
				actor,
				epoch,
				authority),
			WireCommandKind.UpdatePlayerStatus => new UpdatePlayerStatusCommand(
				operation,
				actor,
				epoch,
				authority,
				KernelDomainWireMapper.FromWirePlayerState(command.PlayerState ?? throw new InvalidOperationException("UpdatePlayerStatus command lacks player state"))),
			WireCommandKind.ResetPlayers => new ResetPlayersCommand(
				operation,
				actor,
				epoch,
				authority),
			WireCommandKind.SetPlayerCarry => new SetPlayerCarryCommand(
				operation,
				actor,
				epoch,
				authority,
				command.CarrierSteamId,
				command.CarriedSteamId),
			WireCommandKind.ClearPlayerCarry => new ClearPlayerCarryCommand(
				operation,
				actor,
				epoch,
				authority,
				command.CarrierSteamId,
				command.CarriedSteamId),
			WireCommandKind.RecordEnemyBite => EnemyCombatWireMapper.FromWireBiteCommand(
				command.EnemyCombat ?? throw new InvalidOperationException("RecordEnemyBite command lacks enemy combat payload"),
				operation,
				actor,
				epoch,
				authority),
			WireCommandKind.RecordEnemyLunge => EnemyCombatWireMapper.FromWireLungeCommand(
				command.EnemyCombat ?? throw new InvalidOperationException("RecordEnemyLunge command lacks enemy combat payload"),
				operation,
				actor,
				epoch,
				authority),
			WireCommandKind.RecordEnemyEffect => EnemyCombatWireMapper.FromWireEffectCommand(
				command.EnemyCombat ?? throw new InvalidOperationException("RecordEnemyEffect command lacks enemy combat payload"),
				operation,
				actor,
				epoch,
				authority),
			WireCommandKind.UpsertEnemy => new UpsertEnemyCommand(
				operation,
				actor,
				epoch,
				authority,
				KernelDomainWireMapper.FromWireEnemyState(command.EnemyState ?? throw new InvalidOperationException("UpsertEnemy command lacks enemy state"))),
			WireCommandKind.RemoveEnemy => new RemoveEnemyCommand(
				operation,
				actor,
				epoch,
				authority,
				KernelDomainWireMapper.FromWireEntityId(command.EntityId ?? throw new InvalidOperationException("RemoveEnemy command lacks entity id"))),
			WireCommandKind.ResetEnemies => new ResetEnemiesCommand(
				operation,
				actor,
				epoch,
				authority),
			WireCommandKind.UpdateFluidRegion => new UpdateFluidRegionCommand(
				operation,
				actor,
				epoch,
				authority,
				KernelDomainWireMapper.FromWireFluidRegionState(command.FluidState ?? throw new InvalidOperationException("UpdateFluidRegion command lacks fluid state"))),
			WireCommandKind.ResetFluids => new ResetFluidsCommand(
				operation,
				actor,
				epoch,
				authority),
			_ => throw new ArgumentOutOfRangeException(nameof(command.Kind), command.Kind, "unknown wire command kind"),
		};
	}
}
