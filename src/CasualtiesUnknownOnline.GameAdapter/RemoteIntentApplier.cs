using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Owner side of the native inventory intents. The host has validated the
/// requester, the ownership fact and the destination; this replays the native
/// call on the owner's REAL items, so the game's own inventory semantics — slot
/// rules, container capacity and tag guards, the held/worn rules, item
/// animations and sounds — stay the single implementation of the operation.
/// Every discrete step runs inside a RemoteApply scope and ends in the immediate
/// authoritative re-report, so every peer's clone converges without waiting for
/// the 1 Hz character snapshot; a CONTINUOUS step (the while-dragging drain tick,
/// which arrives every frame) does not re-report per frame — that state is
/// un-evented continuous item state and rides the same periodic snapshot the
/// game's own decay and battery drain use.
///
/// A guard refusal is the native refusal: it is logged with the intent, the item
/// and the native reason, and the authoritative report then shows the item where
/// it really is. Nothing here passes <c>force: true</c> or re-implements a rule.
/// </summary>
internal sealed class RemoteIntentApplier(GameAdapterDomains domains)
{
	private readonly ILogger _log = domains.Log;

	public void Apply(RemoteInventoryIntentMsg msg)
	{
		if (!domains.Session.SessionActive || msg.OwnerSteamId != domains.Session.LocalSteamId)
		{
			// The intent is not for this client (or the session ended between the
			// send and the delivery) — observable so a "nothing happened" report
			// has a line to point at.
			_log.LogDebug("[RemoteIntent] {Kind} for owner {Owner} ignored here (session active {Active}, local {Local}).",
				msg.Kind, msg.OwnerSteamId, domains.Session.SessionActive, domains.Session.LocalSteamId);
			return;
		}

		var body = domains.Run.LocalBody;
		if (body == null) // Unity object — ==
		{
			_log.LogWarning("[RemoteIntent] {Kind} skipped: the local body is not ready.", msg.Kind);
			return;
		}

		var item = CarriedItemLocator.FindById(body, msg.ItemInstanceId);
		if (item == null) // Unity object — ==
		{
			_log.LogWarning("[RemoteIntent] {Kind} refused: item {Item} is not carried by the local body.",
				msg.Kind, msg.ItemInstanceId);
			return;
		}

		using (CallContext.Enter(CallContext.Origin.RemoteApply))
		{
			switch (msg.Kind)
			{
				case RemoteInventoryIntentKind.DropItem:
					body.DropItem(item);
					break;
				case RemoteInventoryIntentKind.DropWearable:
					body.DropWearable(item);
					break;
				case RemoteInventoryIntentKind.TakeOutOfContainer:
					ApplyTakeOutOfContainer(item);
					break;
				case RemoteInventoryIntentKind.MoveIntoContainer:
					ApplyMoveIntoContainer(body, item, msg.TargetContainerInstanceId);
					break;
				case RemoteInventoryIntentKind.MoveContainerChildren:
					ApplyMoveContainerChildren(body, item, msg.TargetContainerInstanceId);
					break;
				case RemoteInventoryIntentKind.Drain:
					ApplyDrain(item, msg.Amount);
					break;
				case RemoteInventoryIntentKind.SwapSlots:
					ApplySwapSlots(body, item, msg.TargetSlotIndex);
					break;
				case RemoteInventoryIntentKind.PickUpToSlot:
					ApplyPickUpToSlot(body, item, msg.TargetSlotIndex);
					break;
				default:
					_log.LogWarning("[RemoteIntent] {Kind} is not executable on the owner's body.", msg.Kind);
					return;
			}
		}

		if (msg.Kind.IsContinuousGesture())
		{
			// A per-frame gesture must not trigger a full character re-report per
			// frame — sixty broadcasts a second for one held water container. The
			// drained state is continuous item state, and the game's own un-evented
			// item state (decay, battery charge) already reaches the peers on the
			// periodic character snapshot, so that is the path it takes.
			_log.LogDebug("[RemoteIntent] replayed native {Kind} on item {Item} (amount {Amount}).",
				msg.Kind, msg.ItemInstanceId, msg.Amount);
			return;
		}

		// The owner's own scene changed: the immediate re-report makes every
		// clone (including the remote-backpack viewer) converge now.
		domains.CharacterDataSync.ReportInventoryChanged(body);
		_log.LogInformation("[RemoteIntent] replayed native {Kind} on item {Item} (container {Container}, slot {Slot}).",
			msg.Kind, msg.ItemInstanceId, msg.TargetContainerInstanceId, msg.TargetSlotIndex);
	}

	/// <summary><c>Container.UnloadItem(item, null)</c> (W1): the item leaves its container into the world.</summary>
	private void ApplyTakeOutOfContainer(Item item)
	{
		var parent = item.transform.parent;
		var container = parent != null ? parent.GetComponent<Container>() : null; // Unity object — ==
		if (container == null) // Unity object — ==
		{
			_log.LogWarning("[RemoteIntent] take-out refused: item {Item} is not inside a container on the owner's body.", item.id);
			return;
		}

		container.UnloadItem(item, null);
	}

	/// <summary>The native move pair, evaluated by the container's own guards (weight, tags, nesting, distance).</summary>
	private void ApplyMoveIntoContainer(Body body, Item item, ulong containerInstanceId)
	{
		var containerItem = CarriedItemLocator.FindById(body, containerInstanceId);
		var container = containerItem != null ? containerItem.GetComponent<Container>() : null; // Unity objects — ==
		if (container == null) // Unity object — ==
		{
			_log.LogWarning("[RemoteIntent] move refused: container {Container} is not resolvable on the owner's body.", containerInstanceId);
			return;
		}

		container.UnloadItem(item, null);
		container.LoadItem(item);
		if (item.transform.parent != container.transform) // Unity objects — ==
		{
			_log.LogWarning("[RemoteIntent] move refused by the native container guard: item {Item} did not enter container {Container} (weight, tag restriction or distance).",
				item.id, containerInstanceId);
			return;
		}

		_log.LogInformation("[RemoteIntent] item {Item} entered container {Container} through the native guard.", item.id, containerInstanceId);
	}

	/// <summary>
	/// R5's per-child loop on the owner's real objects (<c>PlayerCamera.cs:1585</c>):
	/// every direct child of the dragged item's OWN container that the target's native
	/// <c>CanHoldItem</c> admits is unloaded from that container and loaded into the
	/// target, one pair at a time — the same guard, the same child order and the same
	/// partial outcome the local gesture would have had. A child the guard refuses
	/// stays where it is and the rest still move, exactly as natively.
	/// </summary>
	private void ApplyMoveContainerChildren(Body body, Item item, ulong containerInstanceId)
	{
		var source = item.GetComponent<Container>();
		var targetItem = CarriedItemLocator.FindById(body, containerInstanceId);
		var target = targetItem != null ? targetItem.GetComponent<Container>() : null; // Unity objects — ==
		if (source == null || target == null) // Unity objects — ==
		{
			_log.LogWarning("[RemoteIntent] container expansion refused: item {Item} is not a container on the owner's body, or container {Container} is not resolvable there.",
				item.id, containerInstanceId);
			return;
		}

		// The children are snapshotted before the first unload: unloading reparents
		// them, and enumerating a transform while it changes skips entries.
		var children = DirectChildren(source);
		var moved = 0;
		var refused = 0;
		foreach (var child in children)
		{
			if (!target.CanHoldItem(child))
			{
				refused++;
				continue;
			}

			source.UnloadItem(child, null);
			target.LoadItem(child);

			// LoadItem returns void and refuses silently (a nested container that still
			// holds something, a container inside a container, or its own final weight,
			// tag and distance test), so the count is taken from what the scene shows —
			// a hook reports only verified writes. A child whose load was refused is left
			// unloaded by the pair that already ran, exactly as the native loop leaves it.
			if (child.transform.parent == target.transform) // Unity objects — ==
			{
				moved++;
			}
			else
			{
				refused++;
			}
		}

		_log.LogInformation("[RemoteIntent] container expansion of item {Item} into container {Container}: {Moved} of {Total} direct child item(s) entered the container; {Refused} did not (the native guard, or a native load refusal).",
			item.id, containerInstanceId, moved, children.Count, refused);
	}

	/// <summary>
	/// <c>WaterContainerItem.Drain</c> (<c>PlayerCamera.cs:1732</c>): the owner removes
	/// this frame's amount from its own stack. The requester sent the amount, not the
	/// per-stack list it computed from the proxy, so the distribution is derived here
	/// — from the stack the item really has.
	/// </summary>
	private void ApplyDrain(Item item, float amount)
	{
		var water = item.GetComponent<WaterContainerItem>();
		if (water == null) // Unity object — ==
		{
			_log.LogWarning("[RemoteIntent] drain refused: item {Item} carries no liquid container on the owner's body.", item.id);
			return;
		}

		water.Drain(water.CalculateDrain(amount));
	}

	private static List<Item> DirectChildren(Container container)
	{
		var children = new List<Item>();
		var parent = container.transform;
		for (var index = 0; index < parent.childCount; index++)
		{
			var child = parent.GetChild(index).GetComponent<Item>();
			if (child != null) // Unity object — ==
			{
				children.Add(child);
			}
		}

		return children;
	}

	/// <summary><c>Body.SwapSlots(slot, body.SlotOf(item))</c> (R8) — both slots are direct slots of the owner's body.</summary>
	private void ApplySwapSlots(Body body, Item item, int targetSlot)
	{
		if (!IsValidSlot(body, targetSlot))
		{
			_log.LogWarning("[RemoteIntent] swap refused: slot {Slot} is outside the owner's body.", targetSlot);
			return;
		}

		if (!body.HoldingItem(item))
		{
			_log.LogWarning("[RemoteIntent] swap refused: item {Item} is not in one of the owner's slots.", item.id);
			return;
		}

		body.SwapSlots(targetSlot, body.SlotOf(item));
	}

	/// <summary>
	/// R9's slot release on the owner's body, in the native order
	/// (<c>PlayerCamera.cs:1614-1629</c>): the held item leaves its slot, the item
	/// occupying the destination slot leaves its own when it is held, and then
	/// <c>Body.PickUpItem(item, slot, false)</c> runs with the native guards — a
	/// full container, a blocked slot or <c>onlyHoldInHands</c> is the native
	/// refusal, never forced through. The step list itself is
	/// <see cref="RemoteIntentSlotRelease"/>, which is covered without a scene.
	/// </summary>
	private void ApplyPickUpToSlot(Body body, Item item, int targetSlot)
	{
		var steps = RemoteIntentSlotRelease.Plan(body.slots.Length, targetSlot, body.HoldingItem(item), body.HoldingItem(targetSlot));
		if (steps.Count == 0)
		{
			_log.LogWarning("[RemoteIntent] slot release refused: slot {Slot} is outside the owner's body.", targetSlot);
			return;
		}

		foreach (var step in steps)
		{
			switch (step)
			{
				case RemoteIntentStep.DropHeldItem:
					body.DropItem(item);
					break;
				case RemoteIntentStep.DropSlotItem:
					body.DropItem(targetSlot);
					break;
				case RemoteIntentStep.PickUp:
					body.PickUpItem(item, targetSlot, false);
					break;
			}
		}

		if (body.GetItem(targetSlot) != item) // Unity objects — ==
		{
			_log.LogWarning("[RemoteIntent] slot release refused by the native rule: item {Item} did not land in slot {Slot} (slot not pickable, hand-only item, or the slot was taken).",
				item.id, targetSlot);
			return;
		}

		_log.LogInformation("[RemoteIntent] item {Item} now holds the owner's slot {Slot}.", item.id, targetSlot);
	}

	private static bool IsValidSlot(Body body, int slot) =>
		RemoteIntentSlotRelease.IsValidSlot(body.slots.Length, slot);
}
