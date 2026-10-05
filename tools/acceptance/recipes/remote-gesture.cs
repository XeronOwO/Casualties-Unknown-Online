// recipe: remote-gesture
// args: mode=s item=s cast=n moved=n owner=s
// serves: remote-inventory-native-parity-rework, remote-fentanyl-injection-and-medical-panel-desync
// returns: ok, mode, item, itemId, itemOwner, itemType, itemHeld, itemParentContainer, castIndex, castKind, castTag, castSlot, moved, dragAfter, ringDistance, ringScaleBefore, ringScaleForced, calls, error, detail
//          probe: leniency, uiScale, radialCircleTag, radialCircleRadius, radialMenuScale, pointerDistanceOverScale, centerButtonCount, centerButtonTag, centerButtonSlot, radialOpen, dragItem, buttonCount
//
// Drives the game's OWN release path for one inventory gesture, in process, with no
// OS-level input: the recipe stages PlayerCamera.dragItem, PlayerCamera.clickPos and
// the uiCasts list, then invokes the private PlayerCamera.HandleReleaseDragging the
// mouse release itself calls (PlayerCamera.cs:1456). CUO's PlayerCameraDragUsePatch
// brackets exactly that invocation, so a remote display proxy becomes a native intent
// on the owner and a local item keeps the native local branch.
//  - mode=list:    every rendered remote proxy (its RemoteInventoryItemId Id/OwnerSteamId)
//                  and every InvButton in the scene, in the order cast=<index> names.
//  - mode=open:    the registered remote-backpack entry for owner (auto = the only other
//                  in-world member); the entry keeps its own line-of-sight gate.
//  - mode=probe:   the radial guard's own inputs — PlayerCamera.inventoryUseLeniency, uiScale,
//                  the centre circle's tag/radius/position, the ring's scale, the pointer's
//                  distance over uiScale — plus the active centre-tagged buttons.
//  - mode=hover:   stage the drag (dragItem + radialOpen) and return, so the game's own frames
//                  open the ring before the release; nothing is forced here.
//  - mode=close:   the game's own CloseContainer plus the remote focus release.
//  - mode=release: dragItem = item ("<instanceId>" for a proxy, "local<slot>" for the
//                  local body's own slot), clickPos moved (1) or not (0) relative to the
//                  real pointer, uiCasts = the cast-th InvButton (-1 = an empty list, -2 = the
//                  radial CENTRE target: the tag the guard reads, the ring staged under the
//                  pointer, its scale forced to one only when the game has not opened it).
((System.Func<string>)(() => {
	const System.Reflection.BindingFlags instanceFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	const System.Reflection.BindingFlags staticFlags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var mode = {{s:mode}};
	var itemArg = {{s:item}};
	var castIndex = {{n:cast}};
	var moved = {{n:moved}};
	var ownerArg = {{s:owner}};
	var camera = PlayerCamera.main;
	if (camera == null) { return "{\"ok\":false,\"error\":\"no-camera\"}"; }
	var findType = new System.Func<string, System.Type>((fullName) => {
		var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
		for (var i = 0; i < assemblies.Length; i++) {
			System.Type candidate = null;
			try { candidate = assemblies[i].GetType(fullName, false); } catch { candidate = null; }
			if (candidate != null) { return candidate; }
		}
		return null;
	});
	var proxyType = findType("CasualtiesUnknownOnline.GameAdapter.Character.RemoteCloneRender");
	var markerType = findType("CasualtiesUnknownOnline.GameAdapter.Character.RemoteInventoryItemId");
	var fieldOf = new System.Func<UnityEngine.Component, string, ulong>((component, name) => {
		var field = component.GetType().GetField(name, instanceFlags);
		return field == null ? 0UL : System.Convert.ToUInt64(field.GetValue(component));
	});
	// The evaluator's Mono REPL refuses a lambda that captures a local, so both helpers take the
	// resolved type as a parameter instead of closing over it.
	var markerOf = new System.Func<System.Type, Item, UnityEngine.Component>((wanted, item) => {
		if (wanted == null) { return null; }
		var components = item.GetComponents<UnityEngine.Component>();
		for (var i = 0; i < components.Length; i++) {
			if (components[i] != null && components[i].GetType() == wanted) { return components[i]; }
		}
		return null;
	});
	var isProxy = new System.Func<System.Type, Item, bool>((wanted, item) => wanted != null && item.GetComponent(wanted) != null);
	var buttonList = new System.Collections.Generic.List<InvButton>();
	var buttonObjects = UnityEngine.Object.FindObjectsOfType(typeof(InvButton));
	for (var i = 0; i < buttonObjects.Length; i++) {
		var button = buttonObjects[i] as InvButton;
		if (button != null) { buttonList.Add(button); }
	}
	var proxyList = new System.Collections.Generic.List<Item>();
	var itemObjects = UnityEngine.Object.FindObjectsOfType(typeof(Item));
	for (var i = 0; i < itemObjects.Length; i++) {
		var item = itemObjects[i] as Item;
		if (item != null && isProxy(proxyType, item)) { proxyList.Add(item); }
	}
	var viewType = findType("CasualtiesUnknownOnline.GameAdapter.RemoteBackpackView");
	var viewOpen = false;
	if (viewType != null) {
		var property = viewType.GetProperty("IsOpen", staticFlags);
		if (property != null) { viewOpen = (bool)property.GetValue(null); }
	}
	var services = CasualtiesUnknownOnline.Runtime.CuoBootstrap.Services;
	var session = services == null ? null : services.GetService(typeof(CasualtiesUnknownOnline.Runtime.Session.SessionService)) as CasualtiesUnknownOnline.Runtime.Session.SessionService;
	if (mode == "list") {
		var proxies = new System.Collections.Generic.List<string>();
		for (var i = 0; i < proxyList.Count; i++) {
			var item = proxyList[i];
			var marker = markerOf(markerType, item);
			var parentContainer = item.transform.parent != null && item.transform.parent.GetComponent<Container>() != null;
			proxies.Add("{\"i\":" + i
				+ ",\"type\":\"" + item.GetType().Name + "\""
				+ ",\"id\":" + (marker == null ? "0" : fieldOf(marker, "Id").ToString(System.Globalization.CultureInfo.InvariantCulture))
				+ ",\"owner\":" + (marker == null ? "0" : fieldOf(marker, "OwnerSteamId").ToString(System.Globalization.CultureInfo.InvariantCulture))
				+ ",\"held\":" + (camera.body != null && camera.body.HoldingItem(item) ? "true" : "false")
				+ ",\"parentContainer\":" + (parentContainer ? "true" : "false")
				+ ",\"parent\":\"" + (item.transform.parent == null ? "none" : item.transform.parent.name) + "\""
				+ ",\"active\":" + (item.gameObject.activeInHierarchy ? "true" : "false") + "}");
		}
		var buttons = new System.Collections.Generic.List<string>();
		for (var i = 0; i < buttonList.Count; i++) {
			var button = buttonList[i];
			var held = button.GetItem();
			var heldMarker = held == null ? null : markerOf(markerType, held);
			buttons.Add("{\"i\":" + i
				+ ",\"slot\":" + button.slot.ToString(System.Globalization.CultureInfo.InvariantCulture)
				+ ",\"center\":" + (button.centerButtons ? "true" : "false")
				+ ",\"tag\":\"" + button.gameObject.tag + "\""
				+ ",\"item\":\"" + (held == null ? "none" : held.GetType().Name) + "\""
				+ ",\"itemId\":" + (heldMarker == null ? "0" : fieldOf(heldMarker, "Id").ToString(System.Globalization.CultureInfo.InvariantCulture))
				+ ",\"itemOwner\":" + (heldMarker == null ? "0" : fieldOf(heldMarker, "OwnerSteamId").ToString(System.Globalization.CultureInfo.InvariantCulture))
				+ ",\"proxy\":" + (held != null && isProxy(proxyType, held) ? "true" : "false")
				+ ",\"active\":" + (button.gameObject.activeInHierarchy ? "true" : "false") + "}");
		}
		var members = new System.Collections.Generic.List<string>();
		if (session != null) {
			foreach (var member in session.Members) {
				members.Add("{\"steamId\":" + member.SteamId.ToString(System.Globalization.CultureInfo.InvariantCulture)
					+ ",\"inWorld\":" + (member.InWorld ? "true" : "false") + "}");
			}
		}
		return "{\"ok\":true,\"mode\":\"list\""
			+ ",\"localSteamId\":" + (session == null ? "0" : session.LocalSteamId.ToString(System.Globalization.CultureInfo.InvariantCulture))
			+ ",\"members\":[" + string.Join(",", members) + "]"
			+ ",\"radialOpen\":" + (camera.radialOpen ? "true" : "false")
			+ ",\"viewOpen\":" + (viewOpen ? "true" : "false")
			+ ",\"dragItem\":\"" + (camera.dragItem == null ? "none" : camera.dragItem.GetType().Name) + "\""
			+ ",\"proxyCount\":" + proxyList.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
			+ ",\"proxies\":[" + string.Join(",", proxies) + "]"
			+ ",\"buttonCount\":" + buttonList.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
			+ ",\"buttons\":[" + string.Join(",", buttons) + "]}";
	}
	if (mode == "probe") {
		var circle = camera.radialCircle;
		var ringScale = camera.radialMenu != null ? camera.radialMenu.localScale.x : -1f;
		var mouseAt = UnityEngine.Input.mousePosition;
		var circlePosition = circle != null ? circle.transform.position : UnityEngine.Vector3.zero;
		var pointerOverScale = (circle != null && PlayerCamera.uiScale > 0f)
			? UnityEngine.Vector2.Distance(mouseAt, circlePosition) / PlayerCamera.uiScale
			: -1f;
		var centerCount = 0;
		var centerTag = "none";
		var centerSlot = -1;
		for (var i = 0; i < buttonList.Count; i++) {
			if (buttonList[i].gameObject.CompareTag("RadialCenter")) {
				centerCount++;
				centerTag = buttonList[i].gameObject.tag;
				centerSlot = buttonList[i].slot;
			}
		}
		var culture = System.Globalization.CultureInfo.InvariantCulture;
		return "{\"ok\":true,\"mode\":\"probe\""
			+ ",\"leniency\":" + PlayerCamera.inventoryUseLeniency.ToString("0.###", culture)
			+ ",\"uiScale\":" + PlayerCamera.uiScale.ToString("0.###", culture)
			+ ",\"radialCircleTag\":\"" + (circle != null ? circle.gameObject.tag : "none") + "\""
			+ ",\"radialCircleRadius\":" + (circle != null ? (circle.rectTransform.sizeDelta.x * 0.5f).ToString("0.###", culture) : "-1")
			+ ",\"radialMenuScale\":" + ringScale.ToString("0.###", culture)
			+ ",\"pointerDistanceOverScale\":" + pointerOverScale.ToString("0.###", culture)
			+ ",\"centerButtonCount\":" + centerCount.ToString(culture)
			+ ",\"centerButtonTag\":\"" + centerTag + "\""
			+ ",\"centerButtonSlot\":" + centerSlot.ToString(culture)
			+ ",\"radialOpen\":" + (camera.radialOpen ? "true" : "false")
			+ ",\"dragItem\":\"" + (camera.dragItem == null ? "none" : camera.dragItem.GetType().Name) + "\""
			+ ",\"viewOpen\":" + (viewOpen ? "true" : "false")
			+ ",\"buttonCount\":" + buttonList.Count.ToString(culture) + "}";
	}
	if (mode == "close") {
		camera.CloseContainer();
		camera.radialOpen = false;
		if (viewType != null) {
			var close = viewType.GetMethod("Close", staticFlags);
			if (close != null) { close.Invoke(null, null); }
		}
		return "{\"ok\":true,\"mode\":\"close\",\"radialOpen\":false}";
	}
	if (mode == "open") {
		if (services == null) { return "{\"ok\":false,\"error\":\"no-services\"}"; }
		if (session == null) { return "{\"ok\":false,\"error\":\"no-session\"}"; }
		var presentation = services.GetService(typeof(CasualtiesUnknownOnline.Runtime.GameAdapter.IRemoteInventoryPresentation)) as CasualtiesUnknownOnline.Runtime.GameAdapter.IRemoteInventoryPresentation;
		if (presentation == null) { return "{\"ok\":false,\"error\":\"no-remote-presentation\"}"; }
		var ownerId = 0UL;
		if (ownerArg != "auto") { ulong.TryParse(ownerArg, out ownerId); }
		if (ownerId == 0UL) {
			var candidates = 0;
			foreach (var member in session.Members) {
				if (member.SteamId != session.LocalSteamId && member.InWorld) { candidates++; ownerId = member.SteamId; }
			}
			if (candidates != 1) { return "{\"ok\":false,\"error\":\"ambiguous-guest\",\"detail\":\"pass the owner SteamId explicitly\"}"; }
		}
		if (!presentation.OpenRemoteBackpack(ownerId, "acceptance")) {
			return "{\"ok\":false,\"error\":\"open-refused\",\"detail\":\"the remote backpack entry refused (line of sight or no render clone)\"}";
		}
		return "{\"ok\":true,\"mode\":\"open\",\"itemOwner\":" + ownerId.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"viewOpen\":true}";
	}
	if (mode != "release" && mode != "hover") { return "{\"ok\":false,\"error\":\"bad-mode\",\"detail\":\"mode must be list, open, close, probe, hover or release\"}"; }
	Item target = null;
	var targetId = 0UL;
	if (itemArg.StartsWith("local", System.StringComparison.Ordinal)) {
		var slot = 0;
		int.TryParse(itemArg.Substring(5), out slot);
		if (camera.body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }
		target = camera.body.GetItem(slot);
		if (target == null) { return "{\"ok\":false,\"error\":\"no-local-item\",\"detail\":\"the local body holds nothing in slot " + slot + "\"}"; }
	} else {
		ulong.TryParse(itemArg, out targetId);
		for (var i = 0; i < proxyList.Count; i++) {
			var marker = markerOf(markerType, proxyList[i]);
			if (marker != null && fieldOf(marker, "Id") == targetId) { target = proxyList[i]; break; }
		}
		if (target == null) { return "{\"ok\":false,\"error\":\"no-proxy\",\"detail\":\"no rendered proxy carries instance id " + itemArg + "\"}"; }
	}
	if (mode == "hover") {
		camera.dragItem = target;
		camera.radialOpen = true;
		// The ring follows the pointer while dragging, and the game turns radialOpen off
		// when the ring sits more than 600 * uiScale from the pointer. Stage it under the
		// pointer so the game's own frames keep the ring open and grow it to one.
		var hoverMouse = UnityEngine.Input.mousePosition;
		if (camera.radialMenu != null) {
			camera.radialMenu.position = new UnityEngine.Vector3(hoverMouse.x, hoverMouse.y, camera.radialMenu.position.z);
		}
		var hoverDistance = -1f;
		if (camera.radialCircle != null && PlayerCamera.uiScale > 0f) {
			hoverDistance = UnityEngine.Vector2.Distance(hoverMouse, camera.radialCircle.transform.position) / PlayerCamera.uiScale;
		}
		return "{\"ok\":true,\"mode\":\"hover\""
			+ ",\"item\":\"" + itemArg + "\""
			+ ",\"itemId\":" + targetId.ToString(System.Globalization.CultureInfo.InvariantCulture)
			+ ",\"itemType\":\"" + target.GetType().Name + "\""
			+ ",\"radialOpen\":true"
			+ ",\"pointerDistanceOverScale\":" + hoverDistance.ToString(System.Globalization.CultureInfo.InvariantCulture)
			+ ",\"radialMenuScale\":" + (camera.radialMenu != null ? camera.radialMenu.localScale.x : -1f).ToString(System.Globalization.CultureInfo.InvariantCulture) + "}";
	}
	var mouse = UnityEngine.Input.mousePosition;
	var casts = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
	var castTag = "none";
	var castSlot = -1;
	var castKind = "none";
	var ringScaleBefore = -1f;
	var ringScaleForced = false;
	if (castIndex == -2) {
		// The radial CENTRE target. The guard (PlayerCamera.cs:1638) reads the gameObject's
		// tag plus the pointer's distance to radialCircle; it never consults InvButton.Overlaps,
		// so the ring's own centre circle — the object the game tags — is a sufficient cast.
		// The run may not move the OS pointer, so the ring is staged under it instead, and the
		// scale is written to one ONLY when the game's own frames have not opened the ring yet
		// (ringScaleForced says which of the two happened).
		InvButton centerButton = null;
		for (var i = 0; i < buttonList.Count; i++) {
			if (buttonList[i].gameObject.CompareTag("RadialCenter")) { centerButton = buttonList[i]; break; }
		}
		var centerObject = centerButton != null
			? centerButton.gameObject
			: (camera.radialCircle != null ? camera.radialCircle.gameObject : null);
		if (centerObject == null) { return "{\"ok\":false,\"error\":\"no-center-target\"}"; }
		var centerHit = new UnityEngine.EventSystems.RaycastResult();
		centerHit.gameObject = centerObject;
		casts.Add(centerHit);
		castKind = "center";
		castTag = centerObject.tag;
		castSlot = centerButton != null ? centerButton.slot : -1;
		if (camera.radialMenu != null) {
			camera.radialMenu.position = new UnityEngine.Vector3(mouse.x, mouse.y, camera.radialMenu.position.z);
			ringScaleBefore = camera.radialMenu.localScale.x;
			if (ringScaleBefore <= PlayerCamera.inventoryUseLeniency) {
				camera.radialMenu.localScale = UnityEngine.Vector3.one;
				ringScaleForced = true;
			}
		}
	}
	else if (castIndex >= 0 && castIndex < buttonList.Count) {
		castKind = "button";
		var button = buttonList[castIndex];
		var hit = new UnityEngine.EventSystems.RaycastResult();
		hit.gameObject = button.gameObject;
		casts.Add(hit);
		castTag = button.gameObject.tag;
		castSlot = button.slot;
	}
	// The native ring guards read the POINTER against the ring's own screen position
	// (InvButton.Overlaps: 152 < d/uiScale <= 281 for a centre-class button), and the run
	// may not move the OS pointer. Staging the ring under the pointer is the equivalent
	// scene setup: the guard still decides, and the gesture still runs the native branch.
	var ringDistance = -1f;
	if (castIndex >= 0) {
		var uiScale = PlayerCamera.uiScale;
		var ring = camera.radialMenu;
		if (ring != null && uiScale > 0f) {
			ring.position = new UnityEngine.Vector3(mouse.x + 200f * uiScale, mouse.y, ring.position.z);
			ringDistance = UnityEngine.Vector2.Distance(ring.position, mouse) / uiScale;
		}
	}
	camera.dragItem = target;
	camera.clickPos = moved == 1
		? new UnityEngine.Vector2(mouse.x + 60f, mouse.y)
		: new UnityEngine.Vector2(mouse.x, mouse.y);
	var release = typeof(PlayerCamera).GetMethod("HandleReleaseDragging", instanceFlags);
	if (release == null) { return "{\"ok\":false,\"error\":\"no-method\"}"; }
	var failure = "none";
	try { release.Invoke(camera, new object[] { casts }); } catch (System.Exception ex) { failure = ex.GetType().Name + ": " + ex.Message; }
	var marker2 = markerOf(markerType, target);
	return "{\"ok\":true,\"mode\":\"release\""
		+ ",\"item\":\"" + itemArg + "\""
		+ ",\"itemId\":" + (marker2 == null ? targetId : fieldOf(marker2, "Id")).ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"itemOwner\":" + (marker2 == null ? "0" : fieldOf(marker2, "OwnerSteamId").ToString(System.Globalization.CultureInfo.InvariantCulture))
		+ ",\"itemType\":\"" + target.GetType().Name + "\""
		+ ",\"itemParentContainer\":" + (target.transform.parent != null && target.transform.parent.GetComponent<Container>() != null ? "true" : "false")
		+ ",\"castIndex\":" + castIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"castKind\":\"" + castKind + "\""
		+ ",\"castTag\":\"" + castTag + "\""
		+ ",\"castSlot\":" + castSlot.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"moved\":" + moved.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"dragAfter\":\"" + (camera.dragItem == null ? "none" : camera.dragItem.GetType().Name) + "\""
		+ ",\"ringDistance\":" + ringDistance.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"ringScaleBefore\":" + ringScaleBefore.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"ringScaleForced\":" + (ringScaleForced ? "true" : "false")
		+ ",\"calls\":\"" + failure + "\"}";
}))()
