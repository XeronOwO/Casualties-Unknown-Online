// recipe: peg-fit
// args: type=s
// serves: row 25 (prosthetic) — the game's own fitting action, on the limb that carries a stump
// returns: ok, type, itemSlot, parentLimb, parentName, stumpLimb, stumpName, attachedBefore, attachedAfter, error, detail
//
// The player's fitting is the peglimb item's own `useLimbAction` (`Item.cs:745-785`): the item is applied to the
// limb whose `connectedLimbs` still holds a dismembered one, and the game attaches the peg to that stump and destroys
// the item. The row reaches it the way the wound view does — select the carrier limb, then `PlayerCamera.ApplyWoundItem`
// — so the fitting that happens is the game's, with its own skill check and its own sound. Reflection-free.
((System.Func<string>)(() => {
	var type = {{s:type}};
	var camera = PlayerCamera.main;
	if (camera == null) { return "{\"ok\":false,\"error\":\"no-camera\"}"; }
	var body = camera.body;
	if (body == null) { return "{\"ok\":false,\"error\":\"no-body\"}"; }
	var item = (Item)null;
	var slot = -1;
	if (body.HoldingItem(body.handSlot) && body.GetItem(body.handSlot) != null && body.GetItem(body.handSlot).id == type) { item = body.GetItem(body.handSlot); slot = body.handSlot; }
	for (var i = 0; i < body.slots.Length && item == null; i++) {
		if (!body.HoldingItem(i)) { continue; }
		var candidate = body.GetItem(i);
		if (candidate != null && candidate.id == type) { item = candidate; slot = i; break; }
	}
	if (item == null) { return "{\"ok\":false,\"error\":\"not-held\",\"detail\":\"no slot holds " + type + "\"}"; }
	var parent = -1;
	var stump = -1;
	for (var i = 0; i < body.limbs.Length && parent < 0; i++) {
		var carrier = body.limbs[i];
		if (carrier == null || carrier.connectedLimbs == null) { continue; }
		for (var c = 0; c < carrier.connectedLimbs.Length; c++) {
			var other = carrier.connectedLimbs[c];
			if (other != null && other.dismembered) { parent = i; stump = System.Array.IndexOf(body.limbs.ToArray(), other); break; }
		}
	}
	if (parent < 0) { return "{\"ok\":false,\"error\":\"no-stump\",\"detail\":\"no limb carries a dismembered connected limb\"}"; }
	var parentLimb = body.limbs[parent];
	var stumpLimb = body.limbs[stump];
	// The prosthetic is a struct with its own `hasProsthetic` flag (`Limb.ProstheticInfo`), so the row reads that flag
	// and the stump's own `dismembered` state rather than comparing the struct with null.
	var before = stumpLimb.prostheticInfo.hasProsthetic;
	camera.selectedLimb = parentLimb;
	camera.ApplyWoundItem(item);
	return "{\"ok\":true,\"type\":\"" + type + "\",\"itemSlot\":" + slot
		+ ",\"parentLimb\":" + parent + ",\"parentName\":\"" + parentLimb.fullName + "\""
		+ ",\"stumpLimb\":" + stump + ",\"stumpName\":\"" + stumpLimb.fullName + "\""
		+ ",\"attachedBefore\":" + (before ? "true" : "false")
		+ ",\"attachedAfter\":" + (stumpLimb.prostheticInfo.hasProsthetic ? "true" : "false")
		+ ",\"stumpDismembered\":" + (stumpLimb.dismembered ? "true" : "false") + "}";
}))()
