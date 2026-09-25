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
/// Every step runs inside a RemoteApply scope and ends in the immediate
/// authoritative re-report, so every peer's clone converges without waiting for
/// the 1 Hz character snapshot.
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
