using System.Collections.Generic;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// The carried-item action flows (use + slot + container sync): the report
/// side (the guest sends one digest per action) is the only remaining surface
/// of this class. Host-side accept/correct is owned by the kernel authority +
/// batch projection; the old host receive methods were removed with the legacy
/// item handlers.
/// </summary>
internal sealed class ItemActionSync(
	ISessionControl session,
	IItemActionWorldAccess world,
	IKernelProtocolControl kernelProtocol)
{
	private readonly ISessionControl _session = session;
	private readonly IItemActionWorldAccess _world = world;
	private readonly IKernelProtocolControl _kernelProtocol = kernelProtocol;

	/// <summary>Guest only: an item was used locally — report the used state (digest evidence) so the host validates and corrects. Host-side uses are the host's own authority, never reported.</summary>
	public void SendItemUse(ulong itemId, CharacterItemMsg item)
	{
		if (_session.Role != SessionRole.Guest || !_session.SessionActive)
		{
			return;
		}

		_kernelProtocol.SendCommand(
			new WireCommand
			{
				Kind = WireCommandKind.ItemUpdateState,
				Identity = ToIdentity(itemId, item),
				Data = ToWireData(item),
			},
			WirePayloadType.ItemUpdateStateCommand);
	}

	/// <summary>Guest only: an item moved slots locally — report the new slot so the host's record stays in sync. Host-side moves are the host's own authority, never reported.</summary>
	public void SendItemSlot(ulong itemId, int slotIndex, CharacterItemMsg item)
	{
		if (_session.Role != SessionRole.Guest || !_session.SessionActive)
		{
			return;
		}

		_kernelProtocol.SendCommand(
			new WireCommand
			{
				Kind = WireCommandKind.ItemUpdateState,
				Identity = ToIdentity(itemId, item),
				Data = ToWireData(item),
			},
			WirePayloadType.ItemUpdateStateCommand);
	}

	/// <summary>Guest only: a carried container's FULL fact changed internally (a
	/// nested-content move — an item shifted inside a backpack or held container).
	/// The parent container is the fact source for its owner's own body, so the
	/// full recursive capture is reported — the host records it and relays it as
	/// the carried-fact event.</summary>
	public void SendItemContainerContent(ulong itemId, CharacterItemMsg item)
	{
		if (_session.Role != SessionRole.Guest || !_session.SessionActive)
		{
			return;
		}

		var children = new List<WireContainerChild>();
		FlattenContainerChildren(item, children);
		_kernelProtocol.SendCommand(
			new WireCommand
			{
				Kind = WireCommandKind.ItemContainerSync,
				Identity = ToIdentity(itemId, item),
				Data = ToWireData(item),
				ContainerChildren = children,
			},
			WirePayloadType.ItemContainerSyncCommand);
	}

	private static WireItemIdentity ToIdentity(ulong itemId, CharacterItemMsg item) =>
		new()
		{
			InstanceId = itemId,
			DefinitionId = item.ItemId,
		};

	private static WireItemData ToWireData(CharacterItemMsg item) =>
		KernelWireMapper.ToWireData(ItemKernelAuthority.ToKernelData(item));

	private static void FlattenContainerChildren(CharacterItemMsg parent, List<WireContainerChild> children)
	{
		foreach (var child in parent.Contents)
		{
			children.Add(new WireContainerChild
			{
				Identity = ToIdentity(child.InstanceId, child),
				ParentItemId = parent.InstanceId,
				Data = ToWireData(child),
			});
			FlattenContainerChildren(child, children);
		}
	}

	/// <summary>
	/// Host only: correct every OTHER member's copy of a used world item — the
	/// user's own copy IS the fact (it just drank), every peer's copy adopts
	/// it via the standard correction path (ItemApplication.OnItemCorrection).
	/// Reliable: a lost correction would leave the world copies diverged until
	/// the next use.
	/// </summary>
	public void SendWorldItemCorrection(ulong exceptSteamId, CharacterItemMsg item)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive || item.InstanceId == 0)
		{
			return;
		}

		// Phase C: the authoritative world-item state change is a kernel
		// UpdateItemState batch. The host broadcast carries the committed batch;
		// the guest projection re-surfaces it as the world correction event.
		_world.UpdateWorldItemState(item.InstanceId, item);
		_world.FireCorrectionLocal(item);
	}
}
