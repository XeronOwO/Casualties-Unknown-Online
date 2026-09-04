using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.HostRules;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// Host-authoritative remote-backpack inventory operations. This is the
/// semantic counterpart of the native remote-backpack gestures: instead of
/// mutating display proxies, the viewer sends one operation intent to the host;
/// the host validates against its authoritative character snapshots and kernel,
/// commits the durable item fact, and records the participant result that makes
/// the owner's own body apply the exact authoritative local change. Take and
/// cross-player transfer-to-local are left to <see cref="PlayerInventoryTakeService"/>.
/// </summary>
internal sealed class PlayerRemoteInventoryService(
	ISessionControl session,
	PacketSender sender,
	PlayerCharacterAccess characters,
	IItemControl items,
	IHostRules hostRules,
	IPlayerInteractionVisibility visibility,
	ItemKernelAuthority kernelAuthority,
	PlayerInteractionResultAuthority resultAuthority,
	ILogger log)
{
	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;
	private readonly PlayerCharacterAccess _characters = characters;
	private readonly IItemControl _items = items;
	private readonly IHostRules _hostRules = hostRules;
	private readonly IPlayerInteractionVisibility _visibility = visibility;
	private readonly ItemKernelAuthority _kernelAuthority = kernelAuthority;
	private readonly PlayerInteractionResultAuthority _resultAuthority = resultAuthority;
	private readonly ILogger _log = log;

	public void SendRemoteInventoryOperation(RemoteInventoryOperationRequestMsg msg)
	{
		if (!_session.SessionActive || !_session.LocalInWorld)
		{
			return;
		}

		if (_session.Role == SessionRole.Host)
		{
			HandleRemoteInventoryOperation(_session.LocalSteamId, msg);
			return;
		}

		_sender.Send(_session.HostSteamId, NetMsg.RemoteInventoryOperationRequest, msg);
	}

	public void HandleRemoteInventoryOperation(ulong requester, RemoteInventoryOperationRequestMsg msg)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive || !_session.LocalInWorld)
		{
			return;
		}

		var owner = msg.OwnerSteamId;
		if (owner == 0 || owner == requester || msg.ItemInstanceId == 0)
		{
			_log.LogWarning("[RemoteInventory] refused {Kind}: invalid owner/requester/item ({Owner}/{Requester}/{Item}).",
				msg.Kind, owner, requester, msg.ItemInstanceId);
			return;
		}

		if (!_hostRules.AllowRemoteInventoryTake)
		{
			_log.LogInformation("[RemoteInventory] refused {Kind}: host has disabled remote inventory manipulation (AllowRemoteInventoryTake=false).",
				msg.Kind);
			return;
		}

		if (!_characters.IsInWorld(owner) || !_characters.IsInWorld(requester))
		{
			_log.LogWarning("[RemoteInventory] refused {Kind}: {Owner} or {Requester} is not in-world.", msg.Kind, owner, requester);
			return;
		}

		if (!_visibility.HasLineOfSight(requester, owner))
		{
			_log.LogInformation("[RemoteInventory] refused {Kind}: {Requester} cannot see {Owner}.", msg.Kind, requester, owner);
			return;
		}

		var source = _characters.GetCharacterData(owner);
		if (source is null)
		{
			_log.LogWarning("[RemoteInventory] refused {Kind}: no character snapshot for {Owner}.", msg.Kind, owner);
			return;
		}

		if (!TryFindItem(source.Items, msg.ItemInstanceId, out var original))
		{
			_log.LogWarning("[RemoteInventory] refused {Kind}: {Owner} has no item instance {Item}.",
				msg.Kind, owner, msg.ItemInstanceId);
			return;
		}

		if (original.SlotIndex < 0)
		{
			_log.LogInformation("[RemoteInventory] refused {Kind}: item {Item} is worn (slot {Slot}) — native remote-backpack operations only cover inventory/container items in this slice.",
				msg.Kind, msg.ItemInstanceId, original.SlotIndex);
			return;
		}

		switch (msg.Kind)
		{
			case RemoteInventoryOperationKind.Drop:
				HandleDrop(requester, owner, source, original);
				break;
			case RemoteInventoryOperationKind.MoveToContainer:
				HandleMoveToContainer(requester, owner, source, original, msg.TargetContainerInstanceId);
				break;
			case RemoteInventoryOperationKind.Pour:
				HandlePour(requester, owner, source, original);
				break;
			default:
				_log.LogWarning("[RemoteInventory] refused unknown operation kind {Kind}.", msg.Kind);
				break;
		}
	}

	private void HandleDrop(ulong requester, ulong owner, CharacterDataMsg source, CharacterItemMsg original)
	{
		var position = source.Position?.ToNetVector2() ?? NetVector2.Zero;
		var cloned = PlayerCharacterAccess.CloneCharacter(source);
		if (!TryRemove(cloned.Items, original.InstanceId, out var removed))
		{
			return;
		}

		if (!TryExecuteItemCommand(BuildDropOrSpawnCommand(owner, original, ItemLocation.World(position.X, position.Y)), out var rejection))
		{
			return;
		}

		_characters.SaveCharacterData(owner, cloned);

		if (owner != _session.LocalSteamId)
		{
			_items.RemoveTransferredItem(owner, original.InstanceId);
		}

		_log.LogInformation("[RemoteInventory] {Requester} dropped {Type} (id {Item}) of {Owner} at ({X:F1},{Y:F1}).",
			requester, removed.ItemId, removed.InstanceId, owner, position.X, position.Y);

		RecordTransfer(requester, owner, 0, removed, 0);
	}

	private void HandleMoveToContainer(ulong requester, ulong owner, CharacterDataMsg source, CharacterItemMsg original, ulong targetContainerId)
	{
		if (targetContainerId == 0 || targetContainerId == original.InstanceId)
		{
			_log.LogWarning("[RemoteInventory] refused MoveToContainer: invalid target container {Target}.", targetContainerId);
			return;
		}

		if (!TryFindItem(source.Items, targetContainerId, out var target))
		{
			_log.LogWarning("[RemoteInventory] refused MoveToContainer: {Owner} has no target container instance {Target}.",
				owner, targetContainerId);
			return;
		}

		var cloned = PlayerCharacterAccess.CloneCharacter(source);
		if (!TryRemove(cloned.Items, original.InstanceId, out var removed))
		{
			return;
		}

		if (!TryAddToContainer(cloned.Items, targetContainerId, removed))
		{
			_log.LogWarning("[RemoteInventory] refused MoveToContainer: target {Target} disappeared while cloning.", targetContainerId);
			return;
		}

		if (!TryFindItem(cloned.Items, targetContainerId, out var clonedTarget))
		{
			_log.LogWarning("[RemoteInventory] refused MoveToContainer: target {Target} is not in the cloned snapshot.", targetContainerId);
			return;
		}

		var sync = new SyncContainerItemsCommand(
			_kernelAuthority.NextOperationId(),
			new ActorId(owner),
			_kernelAuthority.CurrentRunEpoch,
			AuthorityKind.OwnerPredictedHostValidated,
			new ItemIdentity(clonedTarget.InstanceId, clonedTarget.ItemId),
			ItemKernelAuthority.ToKernelData(clonedTarget),
			FlattenContainerChildren(clonedTarget));
		if (!TryExecuteItemCommand(sync, out var rejection))
		{
			return;
		}

		_characters.SaveCharacterData(owner, cloned);

		_log.LogInformation("[RemoteInventory] {Requester} moved {Type} (id {Item}) of {Owner} into container {Target}.",
			requester, removed.ItemId, removed.InstanceId, owner, targetContainerId);

		RecordTransfer(requester, owner, owner, removed, targetContainerId);
	}

	private void HandlePour(ulong requester, ulong owner, CharacterDataMsg source, CharacterItemMsg original)
	{
		if (original.Liquids.Count == 0)
		{
			_log.LogInformation("[RemoteInventory] pour refused: {Owner}'s item {Item} has no liquid.",
				owner, original.InstanceId);
			return;
		}

		var cloned = PlayerCharacterAccess.CloneCharacter(source);
		if (!TryFindAndReplace(cloned.Items, original.InstanceId, out var emptied, static item =>
		{
			item.Liquids = [];
			return item;
		}))
		{
			return;
		}

		var current = _kernelAuthority.FindItem(emptied.InstanceId);
		GameCommand stateCommand = current is null
			? BuildSpawnCommand(owner, emptied, ItemLocation.Carried(new ActorId(owner)))
			: BuildUpdateCommand(owner, emptied);
		if (!TryExecuteItemCommand(stateCommand, out var rejection))
		{
			return;
		}

		_characters.SaveCharacterData(owner, cloned);

		_log.LogInformation("[RemoteInventory] {Requester} poured {Item}'s liquid for {Owner} (was {Before} stack(s)).",
			requester, original.InstanceId, owner, original.Liquids.Count);

		RecordItemState(requester, owner, emptied);
	}

	private GameCommand BuildDropOrSpawnCommand(ulong owner, CharacterItemMsg item, ItemLocation location) =>
		_kernelAuthority.FindItem(item.InstanceId) is null
			? BuildSpawnCommand(owner, item, location)
			: BuildDropCommand(owner, item, location);

	private DropItemCommand BuildDropCommand(ulong owner, CharacterItemMsg item, ItemLocation location)
	{
		var current = _kernelAuthority.FindItem(item.InstanceId);
		return new DropItemCommand(
			_kernelAuthority.NextOperationId(),
			new ActorId(owner),
			_kernelAuthority.CurrentRunEpoch,
			AuthorityKind.OwnerPredictedHostValidated,
			item.InstanceId,
			location,
			current?.Revision ?? 0,
			ItemKernelAuthority.ToKernelData(item));
	}

	private DropItemCommand BuildDropCommand(ulong owner, CharacterItemMsg item, NetVector2 position) =>
		BuildDropCommand(owner, item, ItemLocation.World(position.X, position.Y));

	private SpawnItemCommand BuildSpawnCommand(ulong owner, CharacterItemMsg item, ItemLocation location) =>
		new(
			_kernelAuthority.NextOperationId(),
			new ActorId(owner),
			_kernelAuthority.CurrentRunEpoch,
			AuthorityKind.OwnerPredictedHostValidated,
			new ItemIdentity(item.InstanceId, item.ItemId),
			location,
			0,
			ItemKernelAuthority.ToKernelData(item));

	private UpdateItemStateCommand BuildUpdateCommand(ulong owner, CharacterItemMsg item)
	{
		var current = _kernelAuthority.FindItem(item.InstanceId);
		return new UpdateItemStateCommand(
			_kernelAuthority.NextOperationId(),
			new ActorId(owner),
			_kernelAuthority.CurrentRunEpoch,
			AuthorityKind.OwnerPredictedHostValidated,
			item.InstanceId,
			ItemKernelAuthority.ToKernelData(item),
			current?.Revision ?? 0);
	}

	private bool TryExecuteItemCommand(GameCommand command, out Rejection? rejection)
	{
		if (_kernelAuthority.TryExecuteCommand(command, command.Actor.Value, out _, out rejection))
		{
			return true;
		}

		_log.LogWarning("[RemoteInventory] kernel command rejected: {Reason} ({Message}).",
			rejection!.Reason, rejection.Message);
		return false;
	}

	private void RecordTransfer(ulong actor, ulong from, ulong to, CharacterItemMsg item, ulong targetParentItemId)
	{
		if (!_resultAuthority.TryRecordPlayerInventoryTransfer(
			actor,
			from,
			to,
			PlayerInteractionKernelCodec.FromCharacterItem(item),
			targetParentItemId,
			out _,
			out var rejection))
		{
			_log.LogWarning("[RemoteInventory] transfer result rejected: {Reason} ({Message}).",
				rejection!.Reason, rejection.Message);
		}
	}

	private void RecordItemState(ulong actor, ulong owner, CharacterItemMsg item)
	{
		if (!_resultAuthority.TryRecordPlayerItemUseResult(
			actor,
			owner,
			0,
			item.InstanceId,
			false,
			PlayerInteractionKernelCodec.FromCharacterItem(item),
			null,
			null,
			[],
			[],
			[],
			out _,
			out var rejection))
		{
			_log.LogWarning("[RemoteInventory] item-state result rejected: {Reason} ({Message}).",
				rejection!.Reason, rejection.Message);
		}
	}

	private static List<ContainerChildFact> FlattenContainerChildren(CharacterItemMsg parent)
	{
		var facts = new List<ContainerChildFact>();
		foreach (var child in parent.Contents)
		{
			facts.Add(new ContainerChildFact(
				child.InstanceId,
				child.ItemId,
				parent.InstanceId,
				ItemKernelAuthority.ToKernelData(child)));
			facts.AddRange(FlattenContainerChildren(child));
		}

		return facts;
	}

	private static bool TryFindItem(List<CharacterItemMsg> items, ulong instanceId, out CharacterItemMsg found)
	{
		foreach (var item in items)
		{
			if (item.InstanceId == instanceId)
			{
				found = item;
				return true;
			}

			if (TryFindItem(item.Contents, instanceId, out found))
			{
				return true;
			}
		}

		found = null!;
		return false;
	}

	private static bool TryRemove(List<CharacterItemMsg> items, ulong instanceId, out CharacterItemMsg removed)
	{
		for (var i = 0; i < items.Count; i++)
		{
			if (items[i].InstanceId == instanceId)
			{
				removed = items[i];
				items.RemoveAt(i);
				return true;
			}

			if (TryRemove(items[i].Contents, instanceId, out removed))
			{
				return true;
			}
		}

		removed = null!;
		return false;
	}

	private static bool TryFindAndReplace(
		List<CharacterItemMsg> items,
		ulong instanceId,
		out CharacterItemMsg replaced,
		Func<CharacterItemMsg, CharacterItemMsg> replace)
	{
		for (var i = 0; i < items.Count; i++)
		{
			if (items[i].InstanceId == instanceId)
			{
				replaced = replace(items[i]);
				items[i] = replaced;
				return true;
			}

			if (TryFindAndReplace(items[i].Contents, instanceId, out replaced, replace))
			{
				return true;
			}
		}

		replaced = null!;
		return false;
	}

	private static bool TryAddToContainer(List<CharacterItemMsg> items, ulong containerId, CharacterItemMsg child)
	{
		foreach (var item in items)
		{
			if (item.InstanceId == containerId)
			{
				item.Contents.Add(child);
				return true;
			}

			if (TryAddToContainer(item.Contents, containerId, child))
			{
				return true;
			}
		}

		return false;
	}
}
