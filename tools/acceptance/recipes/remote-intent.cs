// recipe: remote-intent
// args: kind=n owner=n item=n slot=n other=n targetbody=n container=n
// returns: ok, kind, owner, item, slot, other, sent, error, detail
//
// Sends ONE native inventory intent to the client that owns the item, through IPlayerInteractionControl.SendRemoteInventoryIntent: the same call the drag
// capture makes when a player drags inside a displayed backpack. The capture half is pinned by tests; this drives the OWNER half on a real session, which is
// what "rearranging inside a displayed backpack never loses one" judges. `kind` is the wire number (1 DropItem, 3 TakeOutOfContainer, 4 MoveIntoContainer,
// 5 SwapSlots, 6 PickUpToSlot, 7 TransferToBody) because this evaluator refuses an enum-typed local built from a string.
((System.Func<string>)(() => {
	var kind = {{n:kind}};
	var owner = (ulong)({{n:owner}});
	var item = (ulong)({{n:item}});
	var slot = {{n:slot}};
	var other = (ulong)({{n:other}});
	var targetBody = (ulong)({{n:targetbody}});
	var container = (ulong)({{n:container}});
	var control = (CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction.IPlayerInteractionControl)CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction.IPlayerInteractionControl));
	if (control == null) { return "{\"ok\":false,\"error\":\"no-interaction-control\"}"; }
	var message = new CasualtiesUnknownOnline.Runtime.Protocol.Messages.RemoteInventoryIntentMsg();
	message.Kind = (CasualtiesUnknownOnline.Runtime.Protocol.Messages.RemoteInventoryIntentKind)kind;
	message.OwnerSteamId = owner;
	message.ItemInstanceId = item;
	message.SlotSelection = slot;
	message.TargetSlotIndex = slot;
	message.TargetItemInstanceId = other;
	message.TargetBodySteamId = targetBody;
	message.TargetContainerInstanceId = container;
	control.SendRemoteInventoryIntent(message);
	return "{\"ok\":true,\"kind\":" + kind + ",\"owner\":" + owner + ",\"item\":" + item
		+ ",\"slot\":" + slot + ",\"other\":" + other + ",\"sent\":true}";
}))()
