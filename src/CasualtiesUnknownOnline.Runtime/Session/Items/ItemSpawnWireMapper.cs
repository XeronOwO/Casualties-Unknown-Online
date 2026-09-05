using System;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Protocol.Wire;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// Item-spawn wire mapping: the transient initial-drop presentation fields
/// (velocity, rotation, fresh-drop flag, angular velocity) ride the kernel
/// command/event DTOs so a peer can materialize a spawned world item in the
/// same phase as the originator. Kept separate from the general
/// <see cref="KernelWireMapper"/> so the one-file/line architecture gate stays
/// under its limit while the spawn family remains discoverable in one place.
/// </summary>
internal static class ItemSpawnWireMapper
{
	internal static WireEvent ToWireEvent(ItemSpawnedEvent spawned) =>
		new()
		{
			Kind = WireEventKind.ItemSpawned,
			Identity = KernelWireMapper.ToWireIdentity(spawned.Identity),
			NewRevision = spawned.Revision,
			NewLocation = KernelWireMapper.ToWireLocation(spawned.Location),
			NewData = spawned.Data is null ? null : KernelWireMapper.ToWireData(spawned.Data.Value),
			VelocityX = spawned.VelocityX,
			VelocityY = spawned.VelocityY,
			Rotation = spawned.Rotation,
			FreshItemDrop = spawned.FreshItemDrop,
			AngularVelocity = spawned.AngularVelocity,
		};

	internal static ItemSpawnedEvent FromWireEvent(WireEvent @event) =>
		new(
			KernelWireMapper.FromWireIdentity(@event.Identity),
			@event.NewRevision,
			KernelWireMapper.FromWireLocation(@event.NewLocation ?? new WireItemLocation { Kind = WireItemLocationKind.Terminal }),
			@event.NewData is null ? null : KernelWireMapper.FromWireData(@event.NewData),
			@event.VelocityX,
			@event.VelocityY,
			@event.Rotation,
			@event.FreshItemDrop,
			@event.AngularVelocity);

	internal static SpawnItemCommand FromWireCommand(WireCommand command, OperationId operation, ActorId actor, RunEpoch epoch, AuthorityKind authority, ItemIdentity identity) =>
		new(
			operation,
			actor,
			epoch,
			authority,
			identity,
			KernelWireMapper.FromWireLocation(command.Location ?? throw new InvalidOperationException("spawn command lacks location")),
			0,
			command.Data is null ? null : KernelWireMapper.FromWireData(command.Data),
			command.VelocityX,
			command.VelocityY,
			command.Rotation,
			command.FreshItemDrop,
			command.AngularVelocity);
}
