// recipe: remote-view-items
// args: mode=s target=s name=s
// serves: 1.0-acceptance-failure-paths-and-update-replay — the remote backpack refresh row
//         (remote-backpack-refresh: a change in the host's open backpack must reach the
//         member's open container window, and an arriving slot must pop on it)
// returns: ok, mode, open, focused, container, window, items, error, detail
//
// Opens, reads or closes the native remote-backpack view from inside the member's
// process. The view and the item markers are internal to the adapter, so both are
// looked up by name (the give-probe shape); the game's own Body/Item/Container/
// PlayerCamera types are referenced normally, the same way remote-hand does.
//
// `window` is the item list the open container window currently draws (the
// containerUnloadButtons the native window rebuilt); `items` is every marked item
// under the focused remote body — the two readings that separate the window's
// refresh from the clone's own body update. `container` is the marker id of the
// container the window is showing.
//
// Opening here is the presentation seam the coordinator calls
// (RemoteBackpackView.Open), not the UI row: the row is about the open window's
// refresh, and the UI row's line-of-sight/rule checks are not what this case judges.
((System.Func<string>)(() => {
	var mode = {{s:mode}};
	var targetText = {{s:target}};
	var displayName = {{s:name}};
	var target = 0UL;
	if (!string.IsNullOrEmpty(targetText) && targetText != "none") { target = System.Convert.ToUInt64(targetText); }

	var findType = new System.Func<string, System.Type>((simpleName) => {
		var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
		for (var i = 0; i < assemblies.Length; i++) {
			System.Type[] types;
			try { types = assemblies[i].GetTypes(); } catch { continue; }
			for (var t = 0; t < types.Length; t++) { if (types[t].Name == simpleName) { return types[t]; } }
		}
		return null;
	});

	const System.Reflection.BindingFlags anyStatic = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
	const System.Reflection.BindingFlags anyInstance = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

	var viewType = findType("RemoteBackpackView");
	var markerType = findType("RemoteInventoryItemId");
	if (viewType == null) { return "{\"ok\":false,\"error\":\"no-RemoteBackpackView\"}"; }
	if (markerType == null) { return "{\"ok\":false,\"error\":\"no-RemoteInventoryItemId\"}"; }
	var openMethod = viewType.GetMethod("Open", anyStatic);
	var closeMethod = viewType.GetMethod("Close", anyStatic);
	var isOpenProperty = viewType.GetProperty("IsOpen", anyStatic);
	var focusedProperty = viewType.GetProperty("FocusedSteamId", anyStatic);
	var focusedBodyProperty = viewType.GetProperty("FocusedBody", anyStatic);
	var idField = markerType.GetField("Id", anyInstance);
	var ownerField = markerType.GetField("OwnerSteamId", anyInstance);
	if (idField == null || ownerField == null) { return "{\"ok\":false,\"error\":\"marker-fields-missing\"}"; }
	var invButtonType = findType("InvButton");
	var refItemField = invButtonType == null ? null : invButtonType.GetField("refItem", anyInstance);

	var camera = PlayerCamera.main;
	if (camera == null) { return "{\"ok\":false,\"error\":\"no-camera\"}"; }
	var local = camera.body;
	if (local == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }
	var currentContainerField = camera.GetType().GetField("currentContainer", anyInstance);
	var currentContainerProperty = currentContainerField == null ? camera.GetType().GetProperty("currentContainer", anyInstance) : null;
	var repopulate = camera.GetType().GetMethod("RepopulateContainer", anyInstance);
	var buttonsField = camera.GetType().GetField("containerUnloadButtons", anyInstance);

	// The focused remote body is the clone whose marked items carry the target's
	// SteamId. A body with no rendered item yet is still a candidate: fall back to
	// the only remote body in the scene, which is the same clone the renderer owns.
	var bodies = UnityEngine.Object.FindObjectsOfType<Body>();
	Body remote = null;
	for (var i = 0; i < bodies.Length; i++) {
		var candidate = bodies[i];
		if (candidate == null || candidate == local) { continue; }
		var markers = candidate.GetComponentsInChildren(markerType, true);
		for (var m = 0; m < markers.Length; m++) {
			var owner = ownerField.GetValue(markers[m]);
			if (owner != null && System.Convert.ToUInt64(owner) == target) { remote = candidate; break; }
		}
		if (remote != null) { break; }
	}
	if (remote == null) {
		var count = 0;
		for (var i = 0; i < bodies.Length; i++) {
			if (bodies[i] == null || bodies[i] == local) { continue; }
			remote = bodies[i];
			count++;
		}
		if (count != 1) { remote = null; }
	}
	if (remote == null) { return "{\"ok\":false,\"error\":\"no-remote-body\",\"target\":" + target.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}"; }

	var readJson = new System.Func<string, System.Reflection.PropertyInfo, System.Reflection.PropertyInfo, System.Reflection.PropertyInfo, System.Type, System.Reflection.FieldInfo, System.Reflection.FieldInfo, PlayerCamera, System.Reflection.FieldInfo, System.Reflection.PropertyInfo, System.Reflection.FieldInfo, System.Type, System.Reflection.FieldInfo, string>(
		(readMode, isOpen, focused, focusedBody, marker, idOf, ownerOf, cam, currentField, currentProperty, buttons, buttonType, refItem) => {
			var open = isOpen == null ? false : System.Convert.ToBoolean(isOpen.GetValue(null));
			var focusedId = focused == null ? 0UL : System.Convert.ToUInt64(focused.GetValue(null) ?? 0UL);
			var body = focusedBody == null ? null : focusedBody.GetValue(null) as Body;
			var current = currentField != null ? currentField.GetValue(cam) as Container : (currentProperty != null ? currentProperty.GetValue(cam) as Container : null);
			var containerId = 0UL;
			if (current != null) {
				var currentMarker = current.GetComponent(marker);
				if (currentMarker != null) { containerId = System.Convert.ToUInt64(idOf.GetValue(currentMarker)); }
			}
			var window = "";
			var buttonList = buttons == null ? null : buttons.GetValue(cam) as System.Collections.IList;
			if (buttonList != null && buttonType != null && refItem != null) {
				for (var b = 0; b < buttonList.Count; b++) {
					var button = buttonList[b] as UnityEngine.Component;
					if (button == null) { continue; }
					var inv = button.GetComponent(buttonType);
					if (inv == null) { continue; }
					var item = refItem.GetValue(inv) as Item;
					if (item == null) { continue; }
					var itemMarker = item.GetComponent(marker);
					if (itemMarker == null) { continue; }
					window = window + System.Convert.ToUInt64(idOf.GetValue(itemMarker)).ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + item.id + ":" + System.Convert.ToUInt64(ownerOf.GetValue(itemMarker)).ToString(System.Globalization.CultureInfo.InvariantCulture) + "|";
				}
			}
			var items = "";
			if (body != null) {
				var marked = body.GetComponentsInChildren<Item>(true);
				for (var i = 0; i < marked.Length; i++) {
					var item = marked[i];
					if (item == null) { continue; }
					var itemMarker = item.GetComponent(marker);
					if (itemMarker == null) { continue; }
					items = items + System.Convert.ToUInt64(idOf.GetValue(itemMarker)).ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + item.id + ":" + System.Convert.ToUInt64(ownerOf.GetValue(itemMarker)).ToString(System.Globalization.CultureInfo.InvariantCulture) + "|";
				}
			}
			return "{\"ok\":true,\"mode\":\"" + readMode + "\",\"open\":" + (open ? "true" : "false")
				+ ",\"focused\":" + focusedId.ToString(System.Globalization.CultureInfo.InvariantCulture)
				+ ",\"container\":" + containerId.ToString(System.Globalization.CultureInfo.InvariantCulture)
				+ ",\"window\":\"" + window + "\",\"items\":\"" + items + "\"}";
		});

	if (mode == "open") {
		if (openMethod == null) { return "{\"ok\":false,\"error\":\"no-Open\"}"; }
		try {
			openMethod.Invoke(null, new object[] { remote, target, displayName ?? "", true });
		} catch (System.Exception error) {
			var inner = error.InnerException ?? error;
			return "{\"ok\":false,\"error\":\"open-failed\",\"detail\":\"" + inner.GetType().Name + ": " + inner.Message.Replace("\"", "'") + "\"}";
		}
		// Bind the open container window to the focused clone's own backpack, the way
		// the native window does when the player clicks the container slot.
		var containers = remote.GetComponentsInChildren<Container>(true);
		Container chosen = null;
		for (var c = 0; c < containers.Length; c++) {
			if (containers[c] == null) { continue; }
			var marker = containers[c].GetComponent(markerType);
			if (marker == null) { continue; }
			if (System.Convert.ToUInt64(idField.GetValue(marker)) == 0UL) { continue; }
			chosen = containers[c];
			break;
		}
		if (chosen != null) {
			if (currentContainerField != null) { currentContainerField.SetValue(camera, chosen); }
			else if (currentContainerProperty != null && currentContainerProperty.CanWrite) { currentContainerProperty.SetValue(camera, chosen); }
			if (repopulate != null) { repopulate.Invoke(camera, null); }
		}
		return readJson(mode, isOpenProperty, focusedProperty, focusedBodyProperty, markerType, idField, ownerField, camera, currentContainerField, currentContainerProperty, buttonsField, invButtonType, refItemField);
	}

	if (mode == "close") {
		if (closeMethod != null) { closeMethod.Invoke(null, null); }
		return "{\"ok\":true,\"mode\":\"close\",\"open\":false}";
	}

	if (mode != "read") {
		return "{\"ok\":false,\"error\":\"bad-mode\",\"detail\":\"mode must be open, read or close\"}";
	}

	return readJson(mode, isOpenProperty, focusedProperty, focusedBodyProperty, markerType, idField, ownerField, camera, currentContainerField, currentContainerProperty, buttonsField, invButtonType, refItemField);
}))()
