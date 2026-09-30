// recipe: container-fill
// args: container=s item=s
// serves: guest-container-contents-ghost-drops-on-host, carried-inventory-registration-re-report
// returns: ok, containerType, itemType, containerId, itemId, loaded, canHold, distance, contentsBefore, contentsAfter, children, error, detail
//
// Guest side: loads a carried item into a carried container through the game's own
// Container.LoadItem — the drag-UI entry the CUO container patches hook — so the
// nested-content move travels the real report path (ContainerItemSync's body-side
// branch). The recipe only finds and names the pair; it never writes the parent by
// hand. The container is the first carried item of that type (the body-slot tree is
// searched top-down, so a top-level bag wins over a nested one) and the loaded item
// is the first carried item of its type that is not already inside it.
((System.Func<string>)(() => {
	const System.Reflection.BindingFlags instanceFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var containerType = {{s:container}};
	var itemType = {{s:item}};
	var instanceIdOf = new System.Func<Item, ulong>((item) => {
		var components = item.GetComponents<UnityEngine.Component>();
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
	var insideOf = new System.Func<Item, UnityEngine.Transform, bool>((candidate, root) => {
		var current = candidate.transform.parent;
		while (current != null) {
			if (current == root) { return true; }
			current = current.parent;
		}
		return false;
	});
	Container containerComp = null;
	for (var i = 0; i < items.Count; i++) {
		if (items[i].id != containerType) { continue; }
		var candidate = items[i].GetComponent<Container>();
		if (candidate == null) { continue; }
		containerComp = candidate;
		break;
	}
	if (containerComp == null) {
		return "{\"ok\":false,\"error\":\"no-container\",\"detail\":\"no carried item of type " + containerType + " carries a Container component\"}";
	}
	Item loadItem = null;
	for (var i = 0; i < items.Count; i++) {
		if (items[i].id != itemType) { continue; }
		if (insideOf(items[i], containerComp.transform)) { continue; }
		loadItem = items[i];
		break;
	}
	if (loadItem == null) {
		return "{\"ok\":false,\"error\":\"no-item\",\"detail\":\"no carried item of type " + itemType + " is outside the "
			+ containerType + " container\"}";
	}
	var contentsBefore = 0;
	for (var child = 0; child < containerComp.transform.childCount; child++) {
		if (containerComp.transform.GetChild(child).GetComponent<Item>() != null) { contentsBefore++; }
	}
	var canHold = containerComp.CanHoldItem(loadItem);
	var distance = UnityEngine.Vector2.Distance(loadItem.transform.position, containerComp.transform.position);
	containerComp.LoadItem(loadItem);
	var loaded = loadItem.transform.parent == containerComp.transform;
	if (!loaded) {
		return "{\"ok\":false,\"error\":\"load-refused\",\"detail\":\"LoadItem left " + itemType + " outside the container (canHold="
			+ (canHold ? "true" : "false") + ", distance=" + distance.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + ")\"}";
	}
	var contentsAfter = 0;
	var sb = new System.Text.StringBuilder();
	sb.Append("{\"ok\":true");
	sb.Append(",\"containerType\":\"").Append(containerType).Append("\"");
	sb.Append(",\"itemType\":\"").Append(itemType).Append("\"");
	sb.Append(",\"containerId\":\"").Append(instanceIdOf(containerComp.GetComponent<Item>())).Append("\"");
	sb.Append(",\"itemId\":\"").Append(instanceIdOf(loadItem)).Append("\"");
	sb.Append(",\"loaded\":true");
	sb.Append(",\"canHold\":").Append(canHold ? "true" : "false");
	sb.Append(",\"distance\":").Append(distance.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
	sb.Append(",\"contentsBefore\":").Append(contentsBefore.ToString(System.Globalization.CultureInfo.InvariantCulture));
	sb.Append(",\"children\":[");
	var first = true;
	for (var child = 0; child < containerComp.transform.childCount; child++) {
		var childItem = containerComp.transform.GetChild(child).GetComponent<Item>();
		if (childItem == null) { continue; }
		contentsAfter++;
		if (!first) { sb.Append(","); }
		first = false;
		sb.Append("{\"type\":\"").Append(childItem.id).Append("\",\"id\":\"").Append(instanceIdOf(childItem)).Append("\"}");
	}
	sb.Append("]");
	sb.Append(",\"contentsAfter\":").Append(contentsAfter.ToString(System.Globalization.CultureInfo.InvariantCulture));
	sb.Append("}");
	return sb.ToString();
}))()
