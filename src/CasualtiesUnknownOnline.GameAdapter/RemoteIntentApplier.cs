using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.GameAdapter.World;
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
				case RemoteInventoryIntentKind.UseItem:
					ApplyUseItem(body, item);
					break;
				case RemoteInventoryIntentKind.WearItem:
					ApplyWearItem(body, item);
					break;
				case RemoteInventoryIntentKind.CombineItems:
					ApplyCombineItems(body, msg.ItemInstanceId, msg.TargetItemInstanceId);
					break;
				case RemoteInventoryIntentKind.LoadBattery:
					ApplyLoadBattery(body, msg.ItemInstanceId, msg.TargetItemInstanceId);
					break;
				case RemoteInventoryIntentKind.UnloadBattery:
					ApplyUnloadBattery(item);
					break;
				case RemoteInventoryIntentKind.ToggleFavourite:
					ApplyToggleFavourite(item);
					break;
				case RemoteInventoryIntentKind.GiveToTrader:
					ApplyGiveToTrader(item, msg.TargetTraderPosition);
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

	/// <summary>
	/// <c>Body.UseItem(item)</c> (R10, <c>PlayerCamera.cs:1646</c>): the owner's own
	/// body runs the native use action, so the item's effect, sound and animation
	/// happen where the item is, and the landed item-use report states the result.
	/// The native method refuses a non-usable item silently, so that refusal is named
	/// here instead of looking like a lost intent.
	/// </summary>
	private void ApplyUseItem(Body body, Item item)
	{
		if (!item.Stats.usable)
		{
			_log.LogWarning("[RemoteIntent] use refused: item {Item} is not usable, so the native call would have been a silent no-op.", item.id);
			return;
		}

		body.UseItem(item);
	}

	/// <summary>
	/// <c>Body.WearWearable(item)</c> (R10, <c>PlayerCamera.cs:1642</c>): the native
	/// wear runs on the owner with its own guards — an occupied wear slot, a
	/// dismembered limb and the pickup check all decide there. The wearable flag is
	/// tested first because the native method dereferences the wear slot and the target
	/// limb without testing it.
	/// </summary>
	private void ApplyWearItem(Body body, Item item)
	{
		if (!item.Stats.wearable)
		{
			_log.LogWarning("[RemoteIntent] wear refused: item {Item} is not wearable, so the native call would have dereferenced a wear slot it does not have.", item.id);
			return;
		}

		body.WearWearable(item);
	}

	/// <summary>
	/// <c>Body.CombineItems(target, item)</c> (R6, <c>PlayerCamera.cs:1602</c>): the
	/// native pair runs on the owner's real items, so the guard order is the game's own
	/// — <c>CanCombine</c> admits a gun/magazine pair, a magazine/round pair, a liquid
	/// transfer or the condition merge, and every other pair is a silent no-op. The
	/// committed fact is the landed craft report, which commits ONE report only when the
	/// terminal state actually moved, so a refused pair reports nothing. WHICH item is
	/// the receiver comes from <see cref="RemoteItemInteractionOperands"/>: the native
	/// call takes it first, and a swap here would charge the wrong item silently.
	/// </summary>
	private void ApplyCombineItems(Body body, ulong itemInstanceId, ulong targetItemInstanceId)
	{
		var (receiverId, consumedId) = RemoteItemInteractionOperands.CombineTargets(itemInstanceId, targetItemInstanceId);
		if (ResolveCarried(body, receiverId, "combine") is not { } receiver
			|| ResolveCarried(body, consumedId, "combine") is not { } consumed)
		{
			return;
		}

		body.CombineItems(receiver, consumed);
	}

	/// <summary>
	/// <c>BatteryItem.LoadBattery(item)</c> (R3, <c>PlayerCamera.cs:1550</c>): the
	/// dragged battery's real item enters the target's real battery slot, which charges
	/// the target and destroys the battery. The native method returns silently on an
	/// occupied slot, on a battery larger than the slot allows and on an item that is
	/// not a battery, so the slot's own state is what tells a write from a refusal.
	/// Which item receives comes from <see cref="RemoteItemInteractionOperands"/>.
	/// </summary>
	private void ApplyLoadBattery(Body body, ulong itemInstanceId, ulong targetItemInstanceId)
	{
		var (receiverId, batteryId) = RemoteItemInteractionOperands.BatteryLoadTargets(itemInstanceId, targetItemInstanceId);
		if (ResolveCarried(body, batteryId, "battery load") is not { } battery
			|| ResolveCarried(body, receiverId, "battery load") is not { } receiver)
		{
			return;
		}

		if (receiver.battery == null) // Unity object — ==
		{
			_log.LogWarning("[RemoteIntent] battery load refused: item {Target} carries no battery component on the owner's body.", receiver.id);
			return;
		}

		receiver.battery.LoadBattery(battery);
		if (!receiver.battery.hasBattery)
		{
			_log.LogWarning("[RemoteIntent] battery load refused by the native guard: item {Target} still has no battery (the slot was occupied, or {Battery} is not a battery it takes).",
				receiver.id, battery.id);
			return;
		}

		_log.LogInformation("[RemoteIntent] the native load charged item {Target} from battery {Battery} (the battery item was consumed).", receiver.id, battery.id);
	}

	/// <summary>
	/// <c>BatteryItem.UnloadBattery(false)</c> (R2, <c>PlayerCamera.cs:1545</c>): the
	/// hit item's own battery is ejected into a new item and auto-picked-up by the
	/// owner's body; the item is reported by the immediate character re-report like
	/// every other carried fact. The slot's own state is the verified write.
	/// </summary>
	private void ApplyUnloadBattery(Item item)
	{
		if (item.battery == null) // Unity object — ==
		{
			_log.LogWarning("[RemoteIntent] battery unload refused: item {Item} carries no battery component on the owner's body.", item.id);
			return;
		}

		if (!item.battery.hasBattery)
		{
			_log.LogWarning("[RemoteIntent] battery unload refused by the native guard: item {Item} has no battery in its slot (the native method returns without doing anything).", item.id);
			return;
		}

		item.battery.UnloadBattery(false);
		if (item.battery.hasBattery)
		{
			_log.LogWarning("[RemoteIntent] battery unload did not clear item {Item}'s slot.", item.id);
			return;
		}

		_log.LogInformation("[RemoteIntent] the native unload ejected item {Item}'s battery onto the owner's body.", item.id);
	}

	/// <summary>
	/// The while-dragging <c>favourited</c> store (<c>PlayerCamera.cs:1747</c>): the
	/// owner flips its OWN item's field — the toggle is the native gesture, and both
	/// values are named so the line says what the gesture did even though the field has
	/// no event of its own.
	/// </summary>
	private void ApplyToggleFavourite(Item item)
	{
		var before = item.favourited;
		item.favourited = !before;
		_log.LogInformation("[RemoteIntent] favourite of item {Item} is now {Now} (was {Before}).", item.id, item.favourited, before);
	}

	/// <summary>
	/// <c>TraderScript.GiveItem(item)</c> (R12, <c>PlayerCamera.cs:1663</c>): the trader
	/// the REQUESTER had open is resolved by the position the trade domain already keys
	/// its messages by, because the owner's client has no
	/// <c>PlayerCamera.currentTrader</c>. The credit and the item's destruction are the
	/// game's own, and the trader's own credit total is the verified write.
	/// </summary>
	private void ApplyGiveToTrader(Item item, NetVector2Msg? traderPosition)
	{
		if (traderPosition is null)
		{
			_log.LogWarning("[RemoteIntent] trader hand-in refused: the intent carries no trader position.");
			return;
		}

		if (TraderLocator.FindAt(traderPosition) is not { } trader)
		{
			_log.LogWarning("[RemoteIntent] trader hand-in refused: the owner's scene has no trader at ({X}, {Y}).", traderPosition.X, traderPosition.Y);
			return;
		}

		var creditBefore = trader.totalValueGiven;
		trader.GiveItem(item);
		if (trader.totalValueGiven <= creditBefore)
		{
			_log.LogWarning("[RemoteIntent] trader hand-in refused by the native rule: item {Item} was not accepted (a container that still holds something, a valueless or already-bought item, or the lifetime credit cap).", item.id);
			return;
		}

		_log.LogInformation("[RemoteIntent] the native trader hand-in accepted item {Item} at ({X}, {Y}).", item.id, traderPosition.X, traderPosition.Y);
	}

	/// <summary>The second item operand, resolved on the owner's own body: the host checked the ownership fact, this resolves the live object.</summary>
	private Item? ResolveCarried(Body body, ulong itemInstanceId, string what)
	{
		var target = CarriedItemLocator.FindById(body, itemInstanceId);
		if (target == null) // Unity object — ==
		{
			_log.LogWarning("[RemoteIntent] {What} refused: item {Item} is not carried by the owner's body.", what, itemInstanceId);
		}

		return target;
	}
}
