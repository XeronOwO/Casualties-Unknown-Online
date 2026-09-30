// recipe: container-read
// args: mode=s guest=s
// serves: guest-container-contents-ghost-drops-on-host, carried-inventory-registration-re-report
// returns: ok, mode, local, guest, localCount, itemCount, ownerCount, tableCount, worldCount, carriedCount, containedCount, terminalCount, items, owners, table, world, carried, contained, terminal
//
// One client reads the container/content facts of its own view or of the host's
// authoritative tables:
//  - mode=local (any client): the local body's item tree — every slot occupant, worn
//    item and nested child with its type, instance id and parent item. This is the
//    guest-view half of the contents row and the set the registration capture states.
//  - mode=clone (any client): every rendered remote proxy in this scene (the
//    RemoteInventoryItemId marker) grouped by owner SteamId — what THIS screen shows
//    for the other players; the host-view half of the contents row, read from the
//    proxy tree the ghost-drop fix sanitizes.
//  - mode=host (host): the guest's transfer-table record (ItemArbitration.
//    GetTransferredItems + each id's IsTransferredToGuest guard) with its recursive
//    contents ids and kernel kind, plus the kernel item locations
//    (ItemKernelAuthority.QueryItems): world / carried / contained / terminal. The
//    ghost-drop check reads the world set here — a nested id must never leave the
//    contained half for the world half.
// Judgement discipline: a nested id must be read IN the contained half (or inside a
// table entry's contents) — its mere absence from the world half is not a pass.
((System.Func<string>)(() => {
	const System.Reflection.BindingFlags instanceFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var mode = {{s:mode}};
	var guestArg = {{s:guest}};
	if (mode != "local" && mode != "clone" && mode != "host") {
		return "{\"ok\":false,\"error\":\"bad-mode\",\"detail\":\"mode must be local, clone or host\"}";
	}
	var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
	if (services == null) { return "{\"ok\":false,\"error\":\"no-services\"}"; }
	var session = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.SessionService)) as CasualtiesUnknownOnline.Runtime.Session.SessionService;
	if (session == null) { return "{\"ok\":false,\"error\":\"no-session\"}"; }
	var instanceIdOf = new System.Func<Item, ulong>((probe) => {
		var components = probe.GetComponents<UnityEngine.Component>();
		for (var i = 0; i < components.Length; i++) {
			if (components[i] == null || components[i].GetType().Name != "ItemInstanceId") { continue; }
			var idField = components[i].GetType().GetField("Id", instanceFlags);
			if (idField != null) { return System.Convert.ToUInt64(idField.GetValue(components[i])); }
			var idProperty = components[i].GetType().GetProperty("Id", instanceFlags);
			if (idProperty != null) { return System.Convert.ToUInt64(idProperty.GetValue(components[i], null)); }
			return 0UL;
		}
		return 0UL;
	});
	var markerOf = new System.Func<Item, UnityEngine.Component>((item) => {
		var components = item.GetComponents<UnityEngine.Component>();
		for (var i = 0; i < components.Length; i++) {
			if (components[i] != null && components[i].GetType().Name == "RemoteInventoryItemId") { return components[i]; }
		}
		return null;
	});
	var markerField = new System.Func<UnityEngine.Component, string, ulong>((component, name) => {
		var field = component.GetType().GetField(name, instanceFlags);
		return field == null ? 0UL : System.Convert.ToUInt64(field.GetValue(component));
	});
	var guestId = 0UL;
	if (guestArg != "auto") {
		ulong.TryParse(guestArg, out guestId);
	}
	var sb = new System.Text.StringBuilder();
	sb.Append("{\"ok\":true,\"mode\":\"").Append(mode).Append("\"");
	sb.Append(",\"local\":\"").Append(session.LocalSteamId).Append("\"");
	if (mode == "local") {
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
		sb.Append(",\"localCount\":").Append(items.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
		sb.Append(",\"items\":[");
		for (var i = 0; i < items.Count; i++) {
			if (i > 0) { sb.Append(","); }
			var slot = -1;
			for (var s = 0; s < body.slots.Length; s++) {
				if (body.GetItem(s) == items[i]) { slot = s; break; }
			}
			var parentItem = items[i].transform.parent != null ? items[i].transform.parent.GetComponent<Item>() : null;
			sb.Append("{\"type\":\"").Append(items[i].id).Append("\"");
			sb.Append(",\"id\":\"").Append(instanceIdOf(items[i])).Append("\"");
			sb.Append(",\"slot\":").Append(slot.ToString(System.Globalization.CultureInfo.InvariantCulture));
			sb.Append(",\"parentType\":\"").Append(parentItem != null ? parentItem.id : "").Append("\"");
			sb.Append(",\"children\":[");
			var first = true;
			for (var child = 0; child < items[i].transform.childCount; child++) {
				var childItem = items[i].transform.GetChild(child).GetComponent<Item>();
				if (childItem == null) { continue; }
				if (!first) { sb.Append(","); }
				first = false;
				sb.Append("{\"type\":\"").Append(childItem.id).Append("\",\"id\":\"").Append(instanceIdOf(childItem)).Append("\"}");
			}
			sb.Append("]}");
		}
		sb.Append("]");
	}
	else if (mode == "clone") {
		var found = UnityEngine.Object.FindObjectsOfType(typeof(Item));
		var owners = new System.Collections.Generic.Dictionary<ulong, int>();
		var count = 0;
		var orphanCount = 0;
		sb.Append(",\"items\":[");
		for (var i = 0; i < found.Length; i++) {
			var item = found[i] as Item;
			if (item == null) { continue; }
			var marker = markerOf(item);
			if (marker == null) { continue; }
			var owner = markerField(marker, "OwnerSteamId");
			// The ghost-drop symptom is a proxy that left its clone; record whether this
			// proxy still sits under a RemoteCloneRender and count the ones that do not.
			var underClone = false;
			var rootName = item.name;
			var cursor = item.transform;
			while (cursor != null) {
				if (cursor.GetComponent("RemoteCloneRender") != null) { underClone = true; }
				rootName = cursor.name;
				cursor = cursor.parent;
			}
			if (!underClone) { orphanCount++; }
			if (count > 0) { sb.Append(","); }
			count++;
			if (owners.ContainsKey(owner)) { owners[owner] = owners[owner] + 1; } else { owners[owner] = 1; }
			var parentItem = item.transform.parent != null ? item.transform.parent.GetComponent<Item>() : null;
			sb.Append("{\"owner\":\"").Append(owner).Append("\"");
			sb.Append(",\"type\":\"").Append(item.id).Append("\"");
			sb.Append(",\"id\":\"").Append(markerField(marker, "Id")).Append("\"");
			sb.Append(",\"parentType\":\"").Append(parentItem != null ? parentItem.id : "").Append("\"");
			sb.Append(",\"underClone\":").Append(underClone ? "true" : "false");
			sb.Append(",\"root\":\"").Append(rootName).Append("\"}");
		}
		sb.Append("]");
		sb.Append(",\"orphanCount\":").Append(orphanCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
		sb.Append(",\"itemCount\":").Append(count.ToString(System.Globalization.CultureInfo.InvariantCulture));
		sb.Append(",\"owners\":[");
		var firstOwner = true;
		foreach (var pair in owners) {
			if (!firstOwner) { sb.Append(","); }
			firstOwner = false;
			sb.Append("{\"owner\":\"").Append(pair.Key).Append("\",\"count\":").Append(pair.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append("}");
		}
		sb.Append("]");
		sb.Append(",\"ownerCount\":").Append(owners.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}
	else {
		if (session.Role != CasualtiesUnknownOnline.Runtime.Session.SessionRole.Host) {
			return "{\"ok\":false,\"error\":\"not-host\",\"detail\":\"the authoritative tables are read on the host\"}";
		}
		if (guestId == 0UL) {
			var candidates = 0;
			foreach (var member in session.Members) {
				if (member.SteamId != session.LocalSteamId && member.InWorld) { candidates++; guestId = member.SteamId; }
			}
			if (candidates != 1) {
				return "{\"ok\":false,\"error\":\"ambiguous-guest\",\"detail\":\"pass the guest SteamId explicitly\"}";
			}
		}
		sb.Append(",\"guest\":\"").Append(guestId).Append("\"");
		var arbitration = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.Items.ItemArbitration)) as CasualtiesUnknownOnline.Runtime.Session.Items.ItemArbitration;
		if (arbitration == null) { return "{\"ok\":false,\"error\":\"no-arbitration\"}"; }
		var kernel = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.Items.ItemKernelAuthority)) as CasualtiesUnknownOnline.Runtime.Session.Items.ItemKernelAuthority;
		if (kernel == null) { return "{\"ok\":false,\"error\":\"no-kernel\"}"; }
		var table = arbitration.GetTransferredItems(guestId);
		sb.Append(",\"tableCount\":").Append(table.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
		sb.Append(",\"table\":[");
		for (var i = 0; i < table.Count; i++) {
			if (i > 0) { sb.Append(","); }
			var entry = table[i];
			sb.Append("{\"id\":\"").Append(entry.ItemId).Append("\"");
			sb.Append(",\"type\":\"").Append(entry.Item.ItemId).Append("\"");
			sb.Append(",\"isTransferred\":").Append(arbitration.IsTransferredToGuest(guestId, entry.ItemId) ? "true" : "false");
			sb.Append(",\"contents\":[");
			var ids = new System.Collections.Generic.List<ulong>();
			var stack = new System.Collections.Generic.Stack<CasualtiesUnknownOnline.Runtime.Protocol.Messages.CharacterItemMsg>();
			stack.Push(entry.Item);
			while (stack.Count > 0) {
				var current = stack.Pop();
				for (var c = 0; c < current.Contents.Count; c++) {
					ids.Add(current.Contents[c].InstanceId);
					stack.Push(current.Contents[c]);
				}
			}
			for (var c = 0; c < ids.Count; c++) {
				if (c > 0) { sb.Append(","); }
				sb.Append("\"").Append(ids[c]).Append("\"");
			}
			sb.Append("]}");
		}
		sb.Append("]");
		var kernelItems = kernel.QueryItems();
		var worldCount = 0;
		var carriedCount = 0;
		var containedCount = 0;
		var terminalCount = 0;
		sb.Append(",\"world\":[");
		var first = true;
		foreach (var state in kernelItems.Values) {
			if (state.Location.Kind == CasualtiesUnknownOnline.GameState.Domains.Items.ItemLocationKind.World) {
				if (!first) { sb.Append(","); }
				first = false;
				worldCount++;
				sb.Append("{\"id\":\"").Append(state.Identity.InstanceId).Append("\",\"type\":\"").Append(state.Identity.DefinitionId).Append("\"}");
			}
		}
		sb.Append("]");
		sb.Append(",\"carried\":[");
		first = true;
		foreach (var state in kernelItems.Values) {
			if (state.Location.Kind == CasualtiesUnknownOnline.GameState.Domains.Items.ItemLocationKind.Carried) {
				if (!first) { sb.Append(","); }
				first = false;
				carriedCount++;
				sb.Append("{\"id\":\"").Append(state.Identity.InstanceId).Append("\",\"owner\":\"").Append(state.Location.Owner.Value).Append("\"}");
			}
		}
		sb.Append("]");
		sb.Append(",\"contained\":[");
		first = true;
		foreach (var state in kernelItems.Values) {
			if (state.Location.Kind == CasualtiesUnknownOnline.GameState.Domains.Items.ItemLocationKind.Contained) {
				if (!first) { sb.Append(","); }
				first = false;
				containedCount++;
				sb.Append("{\"id\":\"").Append(state.Identity.InstanceId).Append("\",\"parent\":\"").Append(state.Location.ParentItemId).Append("\"}");
			}
		}
		sb.Append("]");
		sb.Append(",\"terminal\":[");
		first = true;
		foreach (var state in kernelItems.Values) {
			if (state.Location.Kind == CasualtiesUnknownOnline.GameState.Domains.Items.ItemLocationKind.Terminal) {
				if (!first) { sb.Append(","); }
				first = false;
				terminalCount++;
				sb.Append("\"").Append(state.Identity.InstanceId).Append("\"");
			}
		}
		sb.Append("]");
		sb.Append(",\"worldCount\":").Append(worldCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
		sb.Append(",\"carriedCount\":").Append(carriedCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
		sb.Append(",\"containedCount\":").Append(containedCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
		sb.Append(",\"terminalCount\":").Append(terminalCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}
	sb.Append("}");
	return sb.ToString();
}))()
