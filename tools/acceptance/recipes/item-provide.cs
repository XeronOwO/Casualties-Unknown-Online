// recipe: item-provide
// args: type=s mode=s
// serves: guest-container-contents-ghost-drops-on-host, carried-inventory-registration-re-report
// returns: ok, mode, type, found, already, instanceId, slot, picked, parent, onBody, error, detail
//
// Puts a scenario item into the local body through the game's own calls. mode=find
// reports what the body already carries (the starting-supply grant); mode=create
// reproduces the native grant (Utils.Create + Body.PickUpItem(force:true)) when the
// run's startingsupplies setting did not hand the item out; mode=pickup takes the
// nearest matching world item through the native pickup guards. A created item enters
// AFTER the first registration report — the row-2 shape (an id added after the first
// report) — and the run's record states it as the declared setup substitution.
((System.Func<string>)(() => {
	const System.Reflection.BindingFlags instanceFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var type = {{s:type}};
	var mode = {{s:mode}};
	if (mode != "find" && mode != "create" && mode != "pickup") {
		return "{\"ok\":false,\"error\":\"bad-mode\",\"detail\":\"mode must be find, create or pickup\"}";
	}
	var instanceIdOf = new System.Func<Item, ulong>((probe) => {
		var components = probe.GetComponents<UnityEngine.Component>();
		for (var i = 0; i < components.Length; i++) {
			if (components[i] != null && components[i].GetType().Name == "ItemInstanceId") {
				var field = components[i].GetType().GetField("Id", instanceFlags);
				return field == null ? 0UL : System.Convert.ToUInt64(field.GetValue(components[i]));
			}
		}
		return 0UL;
	});
	var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
	if (body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }
	// Every item the body carries: the slot occupants, the worn items, and their nested descendants.
	var items = new System.Collections.Generic.List<Item>();
	for (var slot = 0; slot < body.slots.Length; slot++) {
		var held = body.GetItem(slot);
		if (held != null) { items.Add(held); }
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
	var report = new System.Func<Item, bool, string>((held, already) => {
		var heldSlot = -1;
		for (var i = 0; i < body.slots.Length; i++) {
			if (body.GetItem(i) == held) { heldSlot = i; break; }
		}
		var parent = held.transform.parent != null ? held.transform.parent.name : "";
		var onBody = held.GetComponentInParent<Body>() != null;
		return "{\"ok\":true,\"mode\":\"" + mode + "\",\"type\":\"" + type + "\""
			+ ",\"found\":true,\"already\":" + (already ? "true" : "false")
			+ ",\"instanceId\":\"" + instanceIdOf(held) + "\""
			+ ",\"slot\":" + heldSlot.ToString(System.Globalization.CultureInfo.InvariantCulture)
			+ ",\"picked\":" + (onBody ? "true" : "false") + ",\"parent\":\"" + parent + "\""
			+ ",\"onBody\":" + (onBody ? "true" : "false") + "}";
	});
	Item local = null;
	for (var i = 0; i < items.Count; i++) {
		if (items[i].id == type) { local = items[i]; break; }
	}
	if (mode == "find") {
		if (local == null) { return "{\"ok\":true,\"mode\":\"" + mode + "\",\"type\":\"" + type + "\",\"found\":false}"; }
		return report(local, true);
	}
	var targetSlot = -1;
	for (var i = 0; i < body.slots.Length; i++) {
		if (body.slots[i] != null && body.slots[i].canPickUp && !body.HoldingItem(i)) { targetSlot = i; break; }
	}
	if (targetSlot < 0) { return "{\"ok\":false,\"error\":\"no-free-slot\",\"detail\":\"every body slot is occupied or unusable\"}"; }
	Item item = null;
	var force = false;
	if (mode == "create") {
		var spawned = Utils.Create(type, body.transform.position, 0f);
		if (spawned == null) { return "{\"ok\":false,\"error\":\"create-failed\",\"detail\":\"Utils.Create returned nothing for " + type + "\"}"; }
		item = spawned.GetComponent<Item>();
		if (item == null) {
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
			if (item == null || distance < best) { item = candidate; best = distance; }
		}
		if (item == null) { return "{\"ok\":false,\"error\":\"no-world-item\",\"detail\":\"no world item of type " + type + " is in the scene\"}"; }
	}
	body.PickUpItem(item, targetSlot, force);
	if (!body.HoldingItem(item)) {
		return "{\"ok\":false,\"error\":\"pickup-refused\",\"detail\":\"PickUpItem left " + type + " outside slot "
			+ targetSlot.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\"}";
	}
	return report(item, false);
}))()
