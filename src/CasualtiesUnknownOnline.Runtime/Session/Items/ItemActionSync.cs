using System.Collections.Generic;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// The carried-item action flows (use + slot): the report side (the guest
/// sends one digest per action) and the host receive side (arbitration
/// against the transfer table). A used WORLD item (drinking from a ground
/// canister, #194) takes the world path: the world-table entry adopts the
/// state and every side's scene copy corrects — the craft domain's
/// WorldChange philosophy. Split out of ItemService at the 600-line gate
/// when the world-use branch landed; its ItemService surface is the narrow
/// IItemActionWorldAccess (abstract extraction, the graph stays acyclic).
/// </summary>
internal sealed class ItemActionSync(
	ISessionControl session,
	ItemArbitration arbitration,
	IItemActionWorldAccess world,
	IKernelProtocolControl kernelProtocol,
	ILogger log)
{
	private readonly ISessionControl _session = session;
	private readonly ItemArbitration _arbitration = arbitration;
	private readonly IItemActionWorldAccess _world = world;
	private readonly IKernelProtocolControl _kernelProtocol = kernelProtocol;
	private readonly ILogger _log = log;

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

	public void FireItemUseReceived(ulong sender, ulong itemId, CharacterItemMsg evidence)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		// A WORLD item used in place (drinking from a ground canister, #194):
		// the state changed but the item stays in the world — it has no
		// transfer-table entry (it was never picked up), so the carried-use
		// path below would warn and drop it while the host's world state never
		// updated (the two sides' canisters diverged permanently). The world
		// entry adopts the state and every side's scene copy corrects — the
		// craft domain's WorldChange philosophy.
		if (_world.IsWorldItem(itemId))
		{
			evidence.InstanceId = itemId; // the correction's receivers locate the world copy by it
			_world.UpdateWorldItemState(itemId, evidence);
			_world.FireCorrectionLocal(evidence); // the host's own scene copy adopts it too
			SendWorldItemCorrection(sender, evidence);
			_log.LogInformation("[ItemUse] {Type} (id {ItemId}) — world item, state adopted + peers corrected.", evidence.ItemId, itemId);
			return;
		}

		// The adopted state broadcasts as the carried-fact event; an item with no
		// transfer-table entry yet (the carried-inventory report in flight or
		// lost) falls back to the guest's own report as the fact, broadcast as-is.
		var authoritative = _arbitration.CheckUseEvidence(sender, itemId, evidence) ?? evidence;
		_world.PublishCarriedSyncFor(sender, authoritative);
	}

	public void FireItemSlotReceived(ulong sender, ulong itemId, int slotIndex, CharacterItemMsg item)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		// The recorded slot broadcasts as the carried-fact event; an untracked
		// item falls back to the report's digest evidence (its slot is the new
		// one, SlotKnown), broadcast as-is.
		var authoritative = _arbitration.RecordSlot(sender, itemId, slotIndex, item) ?? item;
		_world.PublishCarriedSyncFor(sender, authoritative);
	}

	public void FireItemContainerContentReceived(ulong sender, ulong itemId, CharacterItemMsg item)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		// The container's nested contents are the owner's local fact (the host
		// record was one action behind), so the report is adopted unconditionally
		// and relayed as the carried-fact event — the same accept-with-correction
		// shape as the use/slot flows.
		var authoritative = _arbitration.RecordContainerContent(sender, itemId, item) ?? item;
		_world.PublishCarriedSyncFor(sender, authoritative);
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
