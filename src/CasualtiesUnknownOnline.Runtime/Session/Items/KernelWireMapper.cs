using System;
using System.Linq;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Protocol.Wire;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// Maps between the deterministic kernel model and the Phase C wire DTOs.
/// This is the only Runtime surface allowed to touch both GameState and
/// Protocol. The mapper is pure and has no network/session state.
/// </summary>
public static class KernelWireMapper
{
	// ===== Kernel -> Wire =====

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
			_ => throw new ArgumentOutOfRangeException(nameof(command.Kind), command.Kind, "unknown wire command kind"),
		};
	}
}
