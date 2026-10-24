// recipe: item-provide
// args: type=s mode=s
// serves: guest-container-contents-ghost-drops-on-host, carried-inventory-registration-re-report,
//         remote-inventory-native-parity-rework (the fixture pass: a body has six slots, so a scene
//         that needs a free one takes an unwanted item out through the game's own drop)
// returns: ok, mode, type, found, already, instanceId, slot, picked, parent, onBody, dropped, error, detail
//
// Puts a scenario item into the local body through the game's own calls. mode=find
// reports what the body already carries (the starting-supply grant); mode=create
// reproduces the native grant (Utils.Create + Body.PickUpItem(force:true)) when the
// run's startingsupplies setting did not hand the item out; mode=pickup takes the
// nearest matching world item through the native pickup guards; mode=drop takes the
// body's own item of that type out again through Body.DropItem(item), which is the
// game's own drop and reports nothing CUO-side unless the carrier sees it. A created
// item enters AFTER the first registration report — the row-2 shape (an id added after
// the first report) — and the run's record states it as the declared setup substitution.
// No inner lambda captures a local: the evaluator's Mono REPL refuses that shape.
((System.Func<string>)(() => {
	const System.Reflection.BindingFlags instanceFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var type = {{s:type}};
	var mode = {{s:mode}};
	if (mode != "find" && mode != "create" && mode != "pickup" && mode != "drop" && mode != "slots") {
		return "{\"ok\":false,\"error\":\"bad-mode\",\"detail\":\"mode must be find, create, pickup, drop or slots\"}";
	}
	var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
	if (body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }
	if (mode == "slots") {
		// The grant's own gate, slot by slot: `create` takes the first slot where the slot exists, `canPickUp` is true and the body
		// is not holding it, and refuses when none qualifies — so the reading that explains a `no-free-slot` is this table, not a
		// guess about which state flips it.
		var table = new System.Text.StringBuilder();
		for (var i = 0; i < body.slots.Length; i++) {
			var slot = body.slots[i];
			if (i > 0) { table.Append(","); }
			table.Append("{\"slot\":").Append(i.ToString(System.Globalization.CultureInfo.InvariantCulture));
			table.Append(",\"null\":").Append(slot == null ? "true" : "false");
			table.Append(",\"canPickUp\":").Append(slot != null && slot.canPickUp ? "true" : "false");
			table.Append(",\"holding\":").Append(body.HoldingItem(i) ? "true" : "false").Append("}");
		}
		return "{\"ok\":true,\"mode\":\"slots\",\"count\":" + body.slots.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)
			+ ",\"slots\":[" + table.ToString() + "]}";
	}
	// Every item the body carries: the slot occupants, the worn items, and their nested descendants.
	var items = new System.Collections.Generic.List<Item>();
	for (var slot = 0; slot < body.slots.Length; slot++) {
		var occupant = body.GetItem(slot);
		if (occupant != null) { items.Add(occupant); }
	}
	for (var limb = 0; limb < body.limbs.Length; limb++) {
		var limbTransform = body.limbs[limb].transform;
		for (var child = 0; child < limbTransform.childCount; child++) {
			var worn = limbTransform.GetChild(child).GetComponent<Item>();
			if (worn != null) { items.Add(worn); }
		}
	}
	for (var i = 0; i < items.Count; i++) {
		var descendants = items[i].GetComponentsInChildren<Item>(true);
		for (var d = 0; d < descendants.Length; d++) {
			if (descendants[d] != items[i] && !items.Contains(descendants[d])) { items.Add(descendants[d]); }
		}
	}
	Item held = null;
	var already = false;
	if (mode == "find") {
		for (var i = 0; i < items.Count; i++) {
			if (items[i].id == type) { held = items[i]; break; }
		}
		if (held == null) { return "{\"ok\":true,\"mode\":\"" + mode + "\",\"type\":\"" + type + "\",\"found\":false}"; }
		already = true;
	}
	else if (mode == "drop") {
		for (var i = 0; i < items.Count; i++) {
			if (items[i].id == type) { held = items[i]; break; }
		}
		if (held == null) { return "{\"ok\":false,\"error\":\"not-carried\",\"detail\":\"the local body carries no " + type + "\"}"; }
		var dropSlot = -1;
		for (var i = 0; i < body.slots.Length; i++) {
			if (body.GetItem(i) == held) { dropSlot = i; break; }
		}
		body.DropItem(held);
		return "{\"ok\":true,\"mode\":\"drop\",\"type\":\"" + type + "\""
			+ ",\"slot\":" + dropSlot.ToString(System.Globalization.CultureInfo.InvariantCulture)
			+ ",\"dropped\":" + (body.HoldingItem(held) ? "false" : "true")
			+ ",\"onBody\":" + (held.GetComponentInParent<Body>() != null ? "true" : "false")
			+ ",\"already\":false}";
	}
	else {
		var targetSlot = -1;
		for (var i = 0; i < body.slots.Length; i++) {
			if (body.slots[i] != null && body.slots[i].canPickUp && !body.HoldingItem(i)) { targetSlot = i; break; }
		}
		if (targetSlot < 0) { return "{\"ok\":false,\"error\":\"no-free-slot\",\"detail\":\"every body slot is occupied or unusable\"}"; }
		var force = false;
		if (mode == "create") {
			var spawned = Utils.Create(type, body.transform.position, 0f);
			if (spawned == null) { return "{\"ok\":false,\"error\":\"create-failed\",\"detail\":\"Utils.Create returned nothing for " + type + "\"}"; }
			held = spawned.GetComponent<Item>();
			if (held == null) {
				UnityEngine.Object.Destroy(spawned);
				return "{\"ok\":false,\"error\":\"create-failed\",\"detail\":\"the created object is not an Item\"}";
			}
			force = true;
		}
		else {
			var found = UnityEngine.Object.FindObjectsOfType(typeof(Item));
			var best = 0f;
			for (var i = 0; i < found.Length; i++) {
				var candidate = found[i] as Item;
				if (candidate == null || candidate.id != type) { continue; }
				if (candidate.GetComponentInParent<Body>() != null) { continue; }
				var distance = UnityEngine.Vector2.Distance(candidate.transform.position, body.transform.position);
				if (held == null || distance < best) { held = candidate; best = distance; }
			}
			if (held == null) { return "{\"ok\":false,\"error\":\"no-world-item\",\"detail\":\"no world item of type " + type + " is in the scene\"}"; }
		}
		body.PickUpItem(held, targetSlot, force);
		if (!body.HoldingItem(held)) {
			return "{\"ok\":false,\"error\":\"pickup-refused\",\"detail\":\"PickUpItem left " + type + " outside slot "
				+ targetSlot.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\"}";
		}
	}
	var heldSlot = -1;
	for (var i = 0; i < body.slots.Length; i++) {
		if (body.GetItem(i) == held) { heldSlot = i; break; }
	}
	var itemId = 0UL;
	var components = held.GetComponents<UnityEngine.Component>();
	for (var i = 0; i < components.Length; i++) {
		if (components[i] == null || components[i].GetType().Name != "ItemInstanceId") { continue; }
		var idField = components[i].GetType().GetField("Id", instanceFlags);
		if (idField != null) {
			itemId = System.Convert.ToUInt64(idField.GetValue(components[i]));
		}
		else {
			var idProperty = components[i].GetType().GetProperty("Id", instanceFlags);
			if (idProperty != null) { itemId = System.Convert.ToUInt64(idProperty.GetValue(components[i], null)); }
		}
		break;
	}
	var parent = held.transform.parent != null ? held.transform.parent.name : "";
	var onBody = held.GetComponentInParent<Body>() != null;
	return "{\"ok\":true,\"mode\":\"" + mode + "\",\"type\":\"" + type + "\""
		+ ",\"found\":true,\"already\":" + (already ? "true" : "false")
		+ ",\"instanceId\":\"" + itemId + "\""
		+ ",\"slot\":" + heldSlot.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"picked\":" + (onBody ? "true" : "false") + ",\"parent\":\"" + parent + "\""
		+ ",\"onBody\":" + (onBody ? "true" : "false") + "}";
}))()
