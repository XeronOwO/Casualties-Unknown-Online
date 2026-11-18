// recipe: item-pickup
// args: mode=s id=s type=s
// serves: item-creation-registration-first row 4 (the free slot and the native pickup of a named world item)
// returns: ok, mode, id, type, slot, droppedId, droppedType, picked, x, y, error, detail
//
// The two native steps row 4's two-sender shape needs:
//   mode=free — drops the first occupied body slot through the game's own Body.DropItem, so a slot
//     the native PickUpItem guard accepts exists again (the starting supplies otherwise fill them).
//   mode=id   — picks up the world item whose readable ItemInstanceId is <id>.
//   mode=type — picks up the nearest world item of <type>.
// The pickup is the real local operation (Body.PickUpItem, force=false — the native guards decide)
// whose report the host must order after the item's creation. mode=free only drops a slot the
// native pickup guard ACCEPTS (`slots[i].canPickUp`), so the freed slot is usable again. The driver
// substitutes every declared argument, so a mode that ignores <type>/<id> still needs a value
// supplied for it (pass a placeholder). The id reader is a delegate over the const flags only: the
// evaluator's Mono REPL refuses a delegate that captures a local. One eval.
((System.Func<string>)(() => {
	const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var mode = {{s:mode}};
	var rawId = {{s:id}};
	var type = {{s:type}};
	var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
	if (body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }
	var idOf = new System.Func<Item, ulong>(delegate(Item probe) {
		var components = probe.GetComponents<UnityEngine.Component>();
		for (var c = 0; c < components.Length; c++) {
			if (components[c] == null || components[c].GetType().Name != "ItemInstanceId") { continue; }
			var idField = components[c].GetType().GetField("Id", flags);
			if (idField != null) { return System.Convert.ToUInt64(idField.GetValue(components[c])); }
			var idProperty = components[c].GetType().GetProperty("Id", flags);
			if (idProperty != null) { return System.Convert.ToUInt64(idProperty.GetValue(components[c], null)); }
			return 0UL;
		}
		return 0UL;
	});
	if (mode == "free") {
		var occupied = 0;
		for (var slot = 0; slot < body.slots.Length; slot++) {
			if (body.slots[slot] == null || !body.HoldingItem(slot)) { continue; }
			occupied = occupied + 1;
			var occupant = body.GetItem(slot);
			if (occupant == null) { continue; }
			if (!body.slots[slot].canPickUp) { continue; } // a slot the native pickup guard refuses: freeing it would reproduce row 4's original failure
			var droppedId = idOf(occupant);
			var droppedType = occupant.id;
			var dropDetail = "";
			try { body.DropItem(slot); }
			catch (System.Exception e) { dropDetail = (e.GetType().Name + ": " + e.Message).Replace("\\", "/").Replace("\"", "'"); }
			if (dropDetail.Length != 0) { return "{\"ok\":false,\"error\":\"drop-failed\",\"detail\":\"" + dropDetail + "\"}"; }
			if (body.HoldingItem(slot)) {
				return "{\"ok\":false,\"error\":\"drop-refused\",\"detail\":\"Body.DropItem left slot " + slot.ToString(inv) + " occupied\"}";
			}
			return "{\"ok\":true,\"mode\":\"free\",\"slot\":" + slot.ToString(inv)
				+ ",\"droppedId\":\"" + droppedId.ToString(inv) + "\",\"droppedType\":\"" + droppedType + "\"}";
		}
		return occupied == 0
			? "{\"ok\":false,\"error\":\"no-occupied-slot\",\"detail\":\"every body slot is empty\"}"
			: "{\"ok\":false,\"error\":\"no-usable-slot\",\"detail\":\"all " + occupied.ToString(inv) + " occupied slot(s) are ones the native pickup guard cannot take\"}";
	}
	if (mode != "id" && mode != "type") { return "{\"ok\":false,\"error\":\"bad-mode\",\"detail\":\"mode must be free, id or type\"}"; }
	var wantedId = 0UL;
	if (mode == "id" && (!ulong.TryParse(rawId, System.Globalization.NumberStyles.None, inv, out wantedId) || wantedId == 0UL)) {
		return "{\"ok\":false,\"error\":\"bad-id\",\"detail\":\"mode=id needs a non-zero decimal item instance id\"}";
	}
	var targetSlot = -1;
	for (var slot = 0; slot < body.slots.Length; slot++) {
		if (body.slots[slot] != null && body.slots[slot].canPickUp && !body.HoldingItem(slot)) { targetSlot = slot; break; }
	}
	if (targetSlot < 0) { return "{\"ok\":false,\"error\":\"no-free-slot\",\"detail\":\"no body slot can take the item\"}"; }
	var found = UnityEngine.Object.FindObjectsOfType(typeof(Item));
	Item held = null;
	var best = 0f;
	for (var i = 0; i < found.Length; i++) {
		var candidate = found[i] as Item;
		if (candidate == null || candidate.GetComponentInParent<Body>() != null) { continue; }
		if (mode == "id") {
			if (idOf(candidate) != wantedId) { continue; }
		}
		else if (candidate.id != type) { continue; }
		var distance = UnityEngine.Vector2.Distance(candidate.transform.position, body.transform.position);
		if (held == null || distance < best) { held = candidate; best = distance; }
	}
	if (held == null) {
		return "{\"ok\":false,\"error\":\"no-world-item\",\"detail\":\"" + (mode == "id"
			? "no world item carries id " + wantedId.ToString(inv)
			: "no world item of type " + type) + "\"}";
	}
	var heldId = idOf(held);
	var pickupDetail = "";
	try { body.PickUpItem(held, targetSlot, false); }
	catch (System.Exception e) { pickupDetail = (e.GetType().Name + ": " + e.Message).Replace("\\", "/").Replace("\"", "'"); }
	if (pickupDetail.Length != 0) { return "{\"ok\":false,\"error\":\"pickup-failed\",\"detail\":\"" + pickupDetail + "\"}"; }
	if (!body.HoldingItem(held)) {
		return "{\"ok\":false,\"error\":\"pickup-refused\",\"detail\":\"PickUpItem left " + held.id + " outside slot " + targetSlot.ToString(inv) + "\"}";
	}
	var heldSlot = -1;
	for (var slot = 0; slot < body.slots.Length; slot++) {
		if (body.GetItem(slot) == held) { heldSlot = slot; break; }
	}
	return "{\"ok\":true,\"mode\":\"" + mode + "\",\"id\":\"" + heldId.ToString(inv)
		+ "\",\"type\":\"" + held.id + "\",\"slot\":" + heldSlot.ToString(inv)
		+ ",\"picked\":true"
		+ ",\"x\":" + held.transform.position.x.ToString("0.###", inv)
		+ ",\"y\":" + held.transform.position.y.ToString("0.###", inv) + "}";
}))()
