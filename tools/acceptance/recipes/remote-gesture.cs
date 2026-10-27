// recipe: remote-gesture
// args: mode=s item=s cast=n moved=n owner=s limb=n frame=s
// serves: remote-inventory-native-parity-rework, remote-fentanyl-injection-and-medical-panel-desync
// returns: ok, mode, item, itemId, itemOwner, itemType, itemHeld, itemParentContainer, castIndex, castKind, castTag, castSlot, moved, dragAfter, ringDistance, ringScaleBefore, ringScaleForced, calls, error, detail
//          wound-view: viewOpen, opened, limbLookingAt, selectedLimbIndex, selectedIsLocal, displayBodyName
//          aim-ring: castIndex, castItemId, buttonOffsetX/Y, mouseX/Y, ringX/Y, focusX/Y, deltaX/Y, targetX/Y
//          while-dragging: castItem, castItemId, castItemProxy, favouritedBefore, favouritedAfter, overlaps, ringStagedDistance
//          medical: limb, limbLookingAt, selectedLimbIndex, selectedIsLocal, woundViewOpen, castIsWoundViewLimb
//          probe: leniency, uiScale, radialCircleTag, radialCircleRadius, radialMenuScale, pointerDistanceOverScale, centerButtonCount, centerButtonTag, centerButtonSlot, radialOpen, dragItem, buttonCount
//          release pair: viewOpen, focusSet, focusedSteamId, castIsBody, castOverlaps, castItem, castItemId, castItemProxy, castItemOwner, castItemParent, castIsContainer, castContentsBefore, castContentsAfter, castPosX, castPosY, dragIsContainer, dragPosX, dragPosY, childCount, children, childInCast, childParentAfter
//
// The release's own target probe. The native branch reads the CAST BUTTON's own item
// (PlayerCamera.cs:1540) and Container.LoadItem then decides on two inputs — CanHoldItem and
// a ten-unit distance (Container.cs:116-151) — refusing silently. Batch 20261006-c drove a
// local control four times without recording either, and the refusal it read could not be
// attributed afterwards. Every release now reports the item it aimed at (identity, display
// proxy, owner, parent, position), whether the ring focus was reading a remote clone, and,
// for the dragged container's own children, both guard inputs plus where the child ended.
// The scene is read before the invoke and the outcome after it, in the same call.
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
//  - mode=wound-view: the acting client's OWN wound view, opened through the game's own
//                  ToggleWoundView, with the view's limb chosen by calling the game's own hit entry
//                  (WoundViewLimb.OnPointerEnter, the method a real pointer-over runs) for limb=<n>.
//                  The view's own Update writes PlayerCamera.selectedLimb from it, which is the fact
//                  the medical release below reads; the answer reports both.
//  - mode=aim-ring: where the FOCUSED body must move, in world space, for the cast-th ring button to sit
//                  under the pointer. The ring is anchored to the focused clone's screen position
//                  (CUO's own anchor) while the game turns the ring off when its x sits more than
//                  600 * uiScale from the pointer's x, so the favourite hold needs the clone near the
//                  pointer — and the run never moves the pointer. This mode reads the game's own camera
//                  map for that move; it changes nothing.
//  - mode=while-dragging: stages the drag and invokes the game's own private HandleWhileDragging with
//                  ONE cast — the cast-th InvButton — because that method takes the frame's UI casts as
//                  its argument and the run cannot move the OS pointer. This is the frame the
//                  favourite store happens in (PlayerCamera.cs:1745); CUO's while-dragging window
//                  brackets exactly this invocation, so the store the native body makes is captured as
//                  an intent for the owner and never left on a proxy. A centre-class button's ring is
//                  staged 200 * uiScale from the pointer, the distance its own Overlaps guard reads.
//  - mode=medical: the wound-view release (R11). The dragged proxy is released onto the WoundViewLimb
//                  object of the limb the view already points at, so the game's own
//                  TryPerformSpecialUIAction -> ApplyWoundItem runs and CUO's release window captures
//                  ApplyToLimb with the ml the item's own limb action computes. It refuses by name
//                  unless the selected limb is this client's OWN body's limb and the view points at it,
//                  so a drive cannot silently treat the wrong limb.
//  - mode=release: dragItem = item ("<instanceId>" for a proxy, "local<slot>" for the
//                  local body's own slot), clickPos moved (1) or not (0) relative to the
//                  real pointer, uiCasts = the cast-th InvButton (-1 = an empty list, -2 = the
//                  radial CENTRE target: the tag the guard reads, the ring staged under the
//                  pointer, its scale forced to one only when the game has not opened it).
//  - mode=hover:   stage the drag (dragItem + radialOpen) and return, so the game's own frames
//                  open the ring before the release; nothing is forced here. With cast>=0 the ring is
//                  moved so the cast-th InvButton sits under the pointer, which is what lets the
//                  client's OWN frames cast that button — the favourite hold needs the frame's own
//                  raycast, and the run never moves the pointer.
((System.Func<string>)(() => {
	const System.Reflection.BindingFlags instanceFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	const System.Reflection.BindingFlags staticFlags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var mode = {{s:mode}};
	var itemArg = {{s:item}};
	var castIndex = {{n:cast}};
	var moved = {{n:moved}};
	var ownerArg = {{s:owner}};
	var limbIndex = (int){{n:limb}};
	// An optional frame captured INSIDE this call, on the same frame as the action. A shrink animation lasts a few frames and a separate
	// capture call lands after it is over (measured 2026-10-27: the ring was already gone), so the row that needs "the ring shrinks AT the
	// teammate" can only be judged from a capture taken here.
	var frameName = {{s:frame}};
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
		var mouseForButtons = UnityEngine.Input.mousePosition;
		var uiScaleForButtons = PlayerCamera.uiScale;
		for (var i = 0; i < buttonList.Count; i++) {
			var button = buttonList[i];
			var held = button.GetItem();
			var heldMarker = held == null ? null : markerOf(markerType, held);
			// The frame's own UI cast reads the pointer, and `InvButton.Overlaps` reads the pointer's
			// distance to the ring — so the geometry the favourite hold depends on is reported here:
			// where the button sits on screen and whether the pointer is inside its rect.
			var buttonRect = button.transform as UnityEngine.RectTransform;
			var pointerInside = buttonRect != null && UnityEngine.RectTransformUtility.RectangleContainsScreenPoint(buttonRect, mouseForButtons, null);
			var overlapDistance = -1f;
			if (camera.radialMenu != null && uiScaleForButtons > 0f) {
				overlapDistance = UnityEngine.Vector2.Distance(camera.radialMenu.position, mouseForButtons) / uiScaleForButtons;
			}
			buttons.Add("{\"i\":" + i
				+ ",\"slot\":" + button.slot.ToString(System.Globalization.CultureInfo.InvariantCulture)
				+ ",\"center\":" + (button.centerButtons ? "true" : "false")
				+ ",\"tag\":\"" + button.gameObject.tag + "\""
				+ ",\"item\":\"" + (held == null ? "none" : held.GetType().Name) + "\""
				+ ",\"itemId\":" + (heldMarker == null ? "0" : fieldOf(heldMarker, "Id").ToString(System.Globalization.CultureInfo.InvariantCulture))
				+ ",\"itemOwner\":" + (heldMarker == null ? "0" : fieldOf(heldMarker, "OwnerSteamId").ToString(System.Globalization.CultureInfo.InvariantCulture))
				+ ",\"proxy\":" + (held != null && isProxy(proxyType, held) ? "true" : "false")
				+ ",\"x\":" + button.transform.position.x.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
				+ ",\"y\":" + button.transform.position.y.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
				+ ",\"pointerInside\":" + (pointerInside ? "true" : "false")
				+ ",\"overlapDistanceOverScale\":" + overlapDistance.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
				+ ",\"active\":" + (button.gameObject.activeInHierarchy ? "true" : "false") + "}");
		}
		var members = new System.Collections.Generic.List<string>();
		if (session != null) {
			foreach (var member in session.Members) {
				members.Add("{\"steamId\":" + member.SteamId.ToString(System.Globalization.CultureInfo.InvariantCulture)
					+ ",\"inWorld\":" + (member.InWorld ? "true" : "false") + "}");
			}
		}
		var focusScreenX = -1f;
		var focusScreenY = -1f;
		if (viewType != null) {
			var listFocusProperty = viewType.GetProperty("FocusedBody", staticFlags);
			var listFocus = listFocusProperty != null ? listFocusProperty.GetValue(null) as Body : null;
			if (listFocus != null && UnityEngine.Camera.main != null) { // Unity objects — ==
				var focusScreen = (UnityEngine.Vector2)UnityEngine.Camera.main.WorldToScreenPoint(listFocus.transform.position);
				focusScreenX = focusScreen.x;
				focusScreenY = focusScreen.y;
			}
		}
		var listMouse = UnityEngine.Input.mousePosition;
		var listScale = PlayerCamera.uiScale;
		var listRing = camera.radialMenu != null ? (UnityEngine.Vector2)camera.radialMenu.position : UnityEngine.Vector2.zero;
		return "{\"ok\":true,\"mode\":\"list\""
			+ ",\"localSteamId\":" + (session == null ? "0" : session.LocalSteamId.ToString(System.Globalization.CultureInfo.InvariantCulture))
			+ ",\"members\":[" + string.Join(",", members) + "]"
			+ ",\"radialOpen\":" + (camera.radialOpen ? "true" : "false")
			+ ",\"viewOpen\":" + (viewOpen ? "true" : "false")
			+ ",\"dragItem\":\"" + (camera.dragItem == null ? "none" : camera.dragItem.GetType().Name) + "\""
			+ ",\"mouseX\":" + listMouse.x.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
			+ ",\"mouseY\":" + listMouse.y.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
			+ ",\"uiScale\":" + listScale.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
			+ ",\"ringX\":" + listRing.x.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
			+ ",\"ringY\":" + listRing.y.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
			+ ",\"ringScale\":" + (camera.radialMenu != null ? camera.radialMenu.localScale.x : -1f).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
			+ ",\"ringToPointerOverScale\":" + (listScale > 0f ? (UnityEngine.Vector2.Distance(listRing, listMouse) / listScale).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) : "-1")
			+ ",\"ringClosesAboveScale\":600"
			+ ",\"focusScreenX\":" + focusScreenX.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
			+ ",\"focusScreenY\":" + focusScreenY.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
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
		if (!string.IsNullOrEmpty(frameName)) {
			var safeFrame = frameName.Replace("/", "-").Replace("\\", "-");
			var frameConfigured = System.Environment.GetEnvironmentVariable("CUO_FRAME_DIR");
			var frameDirectory = string.IsNullOrEmpty(frameConfigured) ? System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cuo-frames") : frameConfigured;
			System.IO.Directory.CreateDirectory(frameDirectory);
			var framePath = System.IO.Path.Combine(frameDirectory, safeFrame + ".png");
			if (System.IO.File.Exists(framePath)) { System.IO.File.Delete(framePath); }
			UnityEngine.ScreenCapture.CaptureScreenshot(framePath);
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
		// The geometry the game's own next frames decide on: the ring is anchored to the focused
		// clone's SCREEN position, and `HandleWhileDragging` turns `radialOpen` off when the ring's x
		// sits more than 600 * uiScale from the pointer's x — so the open reports both, at the instant
		// of the open, instead of leaving the caller to guess where the clone is.
		var openMouse = UnityEngine.Input.mousePosition;
		var openRing = camera.radialMenu != null ? (UnityEngine.Vector2)camera.radialMenu.position : UnityEngine.Vector2.zero;
		var openScale = PlayerCamera.uiScale;
		var openFocusX = -1f;
		var openFocusY = -1f;
		if (viewType != null) {
			var openFocusProperty = viewType.GetProperty("FocusedBody", staticFlags);
			var openFocus = openFocusProperty != null ? openFocusProperty.GetValue(null) as Body : null;
			if (openFocus != null && UnityEngine.Camera.main != null) { // Unity objects — ==
				var openFocusScreen = (UnityEngine.Vector2)UnityEngine.Camera.main.WorldToScreenPoint(openFocus.transform.position);
				openFocusX = openFocusScreen.x;
				openFocusY = openFocusScreen.y;
			}
		}
		return "{\"ok\":true,\"mode\":\"open\",\"itemOwner\":" + ownerId.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"viewOpen\":true"
			+ ",\"radialOpen\":" + (camera.radialOpen ? "true" : "false")
			+ ",\"mouseX\":" + openMouse.x.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
			+ ",\"mouseY\":" + openMouse.y.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
			+ ",\"uiScale\":" + openScale.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
			+ ",\"ringX\":" + openRing.x.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
			+ ",\"ringY\":" + openRing.y.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
			+ ",\"focusScreenX\":" + openFocusX.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
			+ ",\"focusScreenY\":" + openFocusY.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
			+ ",\"ringToPointerOverScale\":" + (openScale > 0f ? (UnityEngine.Vector2.Distance(openRing, openMouse) / openScale).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) : "-1") + "}";
	}
	if (mode == "wound-view") {
		var viewObject = camera.woundView;
		if (viewObject == null) { return "{\"ok\":false,\"error\":\"no-wound-view\"}"; } // Unity object — ==
		var view = viewObject.GetComponent<WoundView>();
		if (view == null) { return "{\"ok\":false,\"error\":\"no-wound-view-component\"}"; } // Unity object — ==
		var opened = false;
		if (!viewObject.activeSelf)
		{
			camera.ToggleWoundView(true);
			opened = true;
		}
		// The limb the view points at is written by the game's OWN hit entry — the method a real
		// pointer-over runs (WoundViewLimb.OnPointerEnter). The view's own Update then writes
		// PlayerCamera.selectedLimb from it, so the answer reports both and the next call reads the
		// selected limb as the view left it.
		var limbObjects = UnityEngine.Object.FindObjectsOfType(typeof(WoundViewLimb));
		WoundViewLimb hit = null;
		for (var i = 0; i < limbObjects.Length; i++) {
			var candidate = limbObjects[i] as WoundViewLimb;
			if (candidate != null && candidate.woundview == view && candidate.limb == limbIndex) { hit = candidate; break; } // Unity object — ==
		}
		if (hit == null) { return "{\"ok\":false,\"error\":\"no-wound-view-limb\",\"detail\":\"no WoundViewLimb for limb " + limbIndex.ToString(System.Globalization.CultureInfo.InvariantCulture) + " belongs to this client's own wound view\"}"; }
		hit.OnPointerEnter(null);
		var selectedIndex = -1;
		var selectedIsLocal = false;
		if (camera.body != null && camera.selectedLimb != null) {
			for (var i = 0; i < camera.body.limbs.Length; i++) {
				if (System.Object.ReferenceEquals(camera.body.limbs[i], camera.selectedLimb)) { selectedIndex = i; break; }
			}
			selectedIsLocal = camera.selectedLimb.transform.IsChildOf(camera.body.transform);
		}
		return "{\"ok\":true,\"mode\":\"wound-view\""
			+ ",\"limb\":" + limbIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)
			+ ",\"opened\":" + (opened ? "true" : "false")
			+ ",\"viewOpen\":" + (viewObject.activeSelf ? "true" : "false")
			+ ",\"limbLookingAt\":" + view.limbLookingAt.ToString(System.Globalization.CultureInfo.InvariantCulture)
			+ ",\"selectedLimbIndex\":" + selectedIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)
			+ ",\"selectedIsLocal\":" + (selectedIsLocal ? "true" : "false") + "}";
	}
	if (mode == "aim-ring") {
		// Where the FOCUSED body must move, in world space, for the cast-th ring button to sit under the
		// pointer. The ring is anchored to the focused clone's screen position (CUO's own anchor), so a
		// clone moved by this delta moves the ring by the same screen delta — and the map from that
		// screen delta to world units is the game's own camera, never a scale this recipe guesses.
		if (viewType == null) { return "{\"ok\":false,\"error\":\"no-view-type\"}"; }
		var aimFocusProperty = viewType.GetProperty("FocusedBody", staticFlags);
		var aimFocus = aimFocusProperty != null ? aimFocusProperty.GetValue(null) as Body : null;
		if (aimFocus == null) { return "{\"ok\":false,\"error\":\"no-focus\",\"detail\":\"open the remote view first\"}"; }
		if (camera.radialMenu == null) { return "{\"ok\":false,\"error\":\"no-ring\"}"; }
		if (castIndex < 0 || castIndex >= buttonList.Count) { return "{\"ok\":false,\"error\":\"bad-cast\",\"detail\":\"cast " + castIndex.ToString(System.Globalization.CultureInfo.InvariantCulture) + " of " + buttonList.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) + " button(s)\"}"; }
		var aimCamera = UnityEngine.Camera.main;
		if (aimCamera == null) { return "{\"ok\":false,\"error\":\"no-camera\"}"; } // Unity object — ==
		var aimButton = buttonList[castIndex];
		var aimMouse = UnityEngine.Input.mousePosition;
		var aimRing = (UnityEngine.Vector2)camera.radialMenu.position;
		var aimButtonPosition = (UnityEngine.Vector2)aimButton.transform.position;
		var aimOffset = aimButtonPosition - aimRing;
		var aimRingTarget = new UnityEngine.Vector2(aimMouse.x - aimOffset.x, aimMouse.y - aimOffset.y);
		var aimFocusPosition = aimFocus.transform.position;
		var aimDepth = aimCamera.WorldToScreenPoint(aimFocusPosition).z;
		var aimWorldAtRing = aimCamera.ScreenToWorldPoint(new UnityEngine.Vector3(aimRing.x, aimRing.y, aimDepth));
		var aimWorldAtTarget = aimCamera.ScreenToWorldPoint(new UnityEngine.Vector3(aimRingTarget.x, aimRingTarget.y, aimDepth));
		var aimDelta = aimWorldAtTarget - aimWorldAtRing;
		var aimInv = System.Globalization.CultureInfo.InvariantCulture;
		return "{\"ok\":true,\"mode\":\"aim-ring\""
			+ ",\"castIndex\":" + castIndex.ToString(aimInv)
			+ ",\"castItemId\":" + (markerOf(markerType, aimButton.GetItem()) == null ? "0" : fieldOf(markerOf(markerType, aimButton.GetItem()), "Id").ToString(aimInv))
			+ ",\"buttonOffsetX\":" + aimOffset.x.ToString("0.###", aimInv)
			+ ",\"buttonOffsetY\":" + aimOffset.y.ToString("0.###", aimInv)
			+ ",\"mouseX\":" + aimMouse.x.ToString("0.###", aimInv)
			+ ",\"mouseY\":" + aimMouse.y.ToString("0.###", aimInv)
			+ ",\"ringX\":" + aimRing.x.ToString("0.###", aimInv)
			+ ",\"ringY\":" + aimRing.y.ToString("0.###", aimInv)
			+ ",\"focusX\":" + aimFocusPosition.x.ToString("0.###", aimInv)
			+ ",\"focusY\":" + aimFocusPosition.y.ToString("0.###", aimInv)
			+ ",\"deltaX\":" + aimDelta.x.ToString("0.###", aimInv)
			+ ",\"deltaY\":" + aimDelta.y.ToString("0.###", aimInv)
			+ ",\"targetX\":" + (aimFocusPosition.x + aimDelta.x).ToString("0.###", aimInv)
			+ ",\"targetY\":" + (aimFocusPosition.y + aimDelta.y).ToString("0.###", aimInv) + "}";
	}
	if (mode != "release" && mode != "hover" && mode != "while-dragging" && mode != "medical") { return "{\"ok\":false,\"error\":\"bad-mode\",\"detail\":\"mode must be list, open, close, probe, wound-view, aim-ring, hover, while-dragging, medical or release\"}"; }
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
		// With a cast the ring is moved on until THAT button sits under the pointer: the frames the
		// client runs itself cast whatever the pointer is over, so this is how a run without pointer
		// control puts an inventory button in front of the game's own while-dragging frame. The ring
		// stays within the 600 * uiScale the game keeps it open at, because a button of the ring is
		// one button-radius away from its centre.
		var castUnderPointer = -1f;
		var castTagUnderPointer = "none";
		var overlapsUnderPointer = false;
		if (castIndex >= 0 && castIndex < buttonList.Count && camera.radialMenu != null) {
			var castButtonForHover = buttonList[castIndex];
			var buttonScreen = castButtonForHover.transform.position;
			var offset = new UnityEngine.Vector3(hoverMouse.x - buttonScreen.x, hoverMouse.y - buttonScreen.y, 0f);
			camera.radialMenu.position = camera.radialMenu.position + offset;
			castTagUnderPointer = castButtonForHover.gameObject.tag;
			castUnderPointer = UnityEngine.Vector2.Distance(castButtonForHover.transform.position, hoverMouse);
			var hoverCasts = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
			var hoverHit = new UnityEngine.EventSystems.RaycastResult();
			hoverHit.gameObject = castButtonForHover.gameObject;
			hoverCasts.Add(hoverHit);
			overlapsUnderPointer = castButtonForHover.Overlaps(hoverCasts);
		}
		var hoverDistance = -1f;
		if (camera.radialCircle != null && PlayerCamera.uiScale > 0f) {
			hoverDistance = UnityEngine.Vector2.Distance(hoverMouse, camera.radialCircle.transform.position) / PlayerCamera.uiScale;
		}
		return "{\"ok\":true,\"mode\":\"hover\""
			+ ",\"item\":\"" + itemArg + "\""
			+ ",\"itemId\":" + targetId.ToString(System.Globalization.CultureInfo.InvariantCulture)
			+ ",\"itemType\":\"" + target.GetType().Name + "\""
			+ ",\"itemOwner\":" + (markerOf(markerType, target) == null ? "0" : fieldOf(markerOf(markerType, target), "OwnerSteamId").ToString(System.Globalization.CultureInfo.InvariantCulture))
			+ ",\"radialOpen\":true"
			+ ",\"castIndex\":" + castIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)
			+ ",\"castTag\":\"" + castTagUnderPointer + "\""
			+ ",\"castUnderPointer\":" + castUnderPointer.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
			+ ",\"castOverlaps\":" + (overlapsUnderPointer ? "true" : "false")
			+ ",\"pointerDistanceOverScale\":" + hoverDistance.ToString(System.Globalization.CultureInfo.InvariantCulture)
			+ ",\"radialMenuScale\":" + (camera.radialMenu != null ? camera.radialMenu.localScale.x : -1f).ToString(System.Globalization.CultureInfo.InvariantCulture) + "}";
	}
	if (mode == "while-dragging") {
		// The game's own while-dragging frame, driven with the frame's UI casts as its argument. The
		// native body is the same one every frame runs (PlayerCamera.cs:1713) and CUO brackets exactly
		// this invocation, so the favourite store the body makes is captured as an intent on the owner.
		if (castIndex < 0 || castIndex >= buttonList.Count) { return "{\"ok\":false,\"error\":\"bad-cast\",\"detail\":\"cast " + castIndex.ToString(System.Globalization.CultureInfo.InvariantCulture) + " of " + buttonList.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) + " button(s)\"}"; }
		var frameButton = buttonList[castIndex];
		var hoveredBefore = frameButton.GetItem();
		var hoveredMarkerBefore = hoveredBefore == null ? null : markerOf(markerType, hoveredBefore);
		var favouritedBefore = hoveredBefore != null && hoveredBefore.favourited;
		camera.dragItem = target;
		var ringStagedDistance = -1f;
		if (frameButton.centerButtons && camera.radialMenu != null) {
			var frameScale = PlayerCamera.uiScale;
			var frameMouse = UnityEngine.Input.mousePosition;
			camera.radialMenu.position = new UnityEngine.Vector3(frameMouse.x + 200f * frameScale, frameMouse.y, camera.radialMenu.position.z);
			ringStagedDistance = UnityEngine.Vector2.Distance(camera.radialMenu.position, frameMouse) / frameScale;
		}
		var frameCasts = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
		var frameHit = new UnityEngine.EventSystems.RaycastResult();
		frameHit.gameObject = frameButton.gameObject;
		frameCasts.Add(frameHit);
		var frameOverlaps = frameButton.Overlaps(frameCasts);
		var whileDragging = typeof(PlayerCamera).GetMethod("HandleWhileDragging", instanceFlags);
		if (whileDragging == null) { return "{\"ok\":false,\"error\":\"no-method\"}"; }
		var frameFailure = "none";
		try { whileDragging.Invoke(camera, new object[] { frameCasts }); } catch (System.Exception ex) { frameFailure = ex.GetType().Name + ": " + ex.Message; }
		var hoveredAfter = frameButton.GetItem();
		return "{\"ok\":true,\"mode\":\"while-dragging\""
			+ ",\"item\":\"" + itemArg + "\""
			+ ",\"itemId\":" + targetId.ToString(System.Globalization.CultureInfo.InvariantCulture)
			+ ",\"itemType\":\"" + target.GetType().Name + "\""
			+ ",\"castIndex\":" + castIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)
			+ ",\"castTag\":\"" + frameButton.gameObject.tag + "\""
			+ ",\"castSlot\":" + frameButton.slot.ToString(System.Globalization.CultureInfo.InvariantCulture)
			+ ",\"overlaps\":" + (frameOverlaps ? "true" : "false")
			+ ",\"ringStagedDistance\":" + ringStagedDistance.ToString(System.Globalization.CultureInfo.InvariantCulture)
			+ ",\"castItem\":\"" + (hoveredBefore == null ? "none" : hoveredBefore.id) + "\""
			+ ",\"castItemId\":" + (hoveredMarkerBefore == null ? "0" : fieldOf(hoveredMarkerBefore, "Id").ToString(System.Globalization.CultureInfo.InvariantCulture))
			+ ",\"castItemProxy\":" + (hoveredBefore != null && isProxy(proxyType, hoveredBefore) ? "true" : "false")
			+ ",\"castItemOwner\":" + (hoveredMarkerBefore == null ? "0" : fieldOf(hoveredMarkerBefore, "OwnerSteamId").ToString(System.Globalization.CultureInfo.InvariantCulture))
			+ ",\"favouritedBefore\":" + (favouritedBefore ? "true" : "false")
			+ ",\"favouritedAfter\":" + (hoveredAfter != null && hoveredAfter.favourited ? "true" : "false")
			+ ",\"dragAfter\":\"" + (camera.dragItem == null ? "none" : camera.dragItem.GetType().Name) + "\""
			+ ",\"calls\":\"" + frameFailure + "\"}";
	}
	if (mode == "medical") {
		// The wound-view release (R11): the dragged proxy goes onto the limb the acting client's own
		// wound view points at. Both coordinates are witnessed before the release: the view's own
		// limbLookingAt (the value the capture reads) and PlayerCamera.selectedLimb, which must be this
		// client's OWN body's limb — the native ApplyWoundItem treats the acting body.
		var medicalViewObject = camera.woundView;
		var medicalView = medicalViewObject != null ? medicalViewObject.GetComponent<WoundView>() : null; // Unity object — ==
		if (medicalView == null) { return "{\"ok\":false,\"error\":\"no-wound-view\"}"; }
		var lookingAt = medicalView.limbLookingAt;
		if (lookingAt != limbIndex) { return "{\"ok\":false,\"error\":\"view-points-elsewhere\",\"detail\":\"the wound view points at limb " + lookingAt.ToString(System.Globalization.CultureInfo.InvariantCulture) + ", not " + limbIndex.ToString(System.Globalization.CultureInfo.InvariantCulture) + " — run mode=wound-view for this limb first\"}"; }
		var medicalSelected = camera.selectedLimb;
		if (camera.body == null || medicalSelected == null || lookingAt < 0 || lookingAt >= camera.body.limbs.Length || !System.Object.ReferenceEquals(camera.body.limbs[lookingAt], medicalSelected)) {
			return "{\"ok\":false,\"error\":\"selected-limb-is-not-this-client-s\",\"detail\":\"selectedLimb is not this client's own limb " + limbIndex.ToString(System.Globalization.CultureInfo.InvariantCulture) + " — the native ApplyWoundItem would treat another body\"}";
		}
		var medicalLimbs = UnityEngine.Object.FindObjectsOfType(typeof(WoundViewLimb));
		WoundViewLimb medicalHit = null;
		for (var i = 0; i < medicalLimbs.Length; i++) {
			var candidate = medicalLimbs[i] as WoundViewLimb;
			if (candidate != null && candidate.woundview == medicalView && candidate.limb == lookingAt) { medicalHit = candidate; break; } // Unity object — ==
		}
		if (medicalHit == null) { return "{\"ok\":false,\"error\":\"no-wound-view-limb\",\"detail\":\"no WoundViewLimb for limb " + lookingAt.ToString(System.Globalization.CultureInfo.InvariantCulture) + " belongs to this client's own wound view\"}"; }
		var medicalCasts = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
		var medicalRaycast = new UnityEngine.EventSystems.RaycastResult();
		medicalRaycast.gameObject = medicalHit.gameObject;
		medicalCasts.Add(medicalRaycast);
		camera.dragItem = target;
		camera.clickPos = moved == 1
			? new UnityEngine.Vector2(UnityEngine.Input.mousePosition.x + 60f, UnityEngine.Input.mousePosition.y)
			: new UnityEngine.Vector2(UnityEngine.Input.mousePosition.x, UnityEngine.Input.mousePosition.y);
		var medicalRelease = typeof(PlayerCamera).GetMethod("HandleReleaseDragging", instanceFlags);
		if (medicalRelease == null) { return "{\"ok\":false,\"error\":\"no-method\"}"; }
		var medicalFailure = "none";
		try { medicalRelease.Invoke(camera, new object[] { medicalCasts }); } catch (System.Exception ex) { medicalFailure = ex.GetType().Name + ": " + ex.Message; }
		var medicalMarker = markerOf(markerType, target);
		return "{\"ok\":true,\"mode\":\"medical\""
			+ ",\"item\":\"" + itemArg + "\""
			+ ",\"itemId\":" + (medicalMarker == null ? targetId : fieldOf(medicalMarker, "Id")).ToString(System.Globalization.CultureInfo.InvariantCulture)
			+ ",\"itemOwner\":" + (medicalMarker == null ? "0" : fieldOf(medicalMarker, "OwnerSteamId").ToString(System.Globalization.CultureInfo.InvariantCulture))
			+ ",\"itemType\":\"" + target.GetType().Name + "\""
			+ ",\"itemProxy\":" + (isProxy(proxyType, target) ? "true" : "false")
			+ ",\"limb\":" + limbIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)
			+ ",\"limbLookingAt\":" + lookingAt.ToString(System.Globalization.CultureInfo.InvariantCulture)
			+ ",\"selectedLimbIndex\":" + lookingAt.ToString(System.Globalization.CultureInfo.InvariantCulture)
			+ ",\"selectedIsLocal\":true"
			+ ",\"woundViewOpen\":" + (medicalViewObject.activeSelf ? "true" : "false")
			+ ",\"castIsWoundViewLimb\":true"
			+ ",\"dragAfter\":\"" + (camera.dragItem == null ? "none" : camera.dragItem.GetType().Name) + "\""
			+ ",\"calls\":\"" + medicalFailure + "\"}";
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
	// The release's own target probe (see the header): the item this release aims at, read
	// through the same gate the native branch uses — the cast's InvButton, its Overlaps band
	// (InvButton.cs:31, a ring/pointer distance the run stages rather than moves), and the
	// GetItem routing the ring focus redirects (InvButtonBodyPatch) — plus Container.LoadItem's
	// two inputs on the dragged container's own children (Container.cs:116-151). A release whose
	// target is a display proxy, or whose child cannot land, is visible here instead of only in
	// the aftermath. `focusSet` says whether the ring reads a remote clone rather than this
	// client's own body: `InvButton.body` is private and patch-redirected, so the focus is the
	// fact that answers it.
	InvButton castButton = null;
	for (var i = 0; i < casts.Count; i++) {
		if (casts[i].gameObject == null) { continue; }
		var candidate = casts[i].gameObject.GetComponent<InvButton>();
		if (candidate != null) { castButton = candidate; break; }
	}
	var castOverlaps = castButton != null && castButton.Overlaps(casts);
	var castButtonItem = castButton == null ? null : castButton.GetItem();
	var castMarker = castButtonItem == null ? null : markerOf(markerType, castButtonItem);
	var castContainer = castButtonItem == null ? null : castButtonItem.GetComponent<Container>();
	var focusSet = false;
	var focusedId = 0UL;
	if (viewType != null) {
		var focusProperty = viewType.GetProperty("FocusedBody", staticFlags);
		if (focusProperty != null) { focusSet = focusProperty.GetValue(null) != null; }
		var focusedProperty = viewType.GetProperty("FocusedSteamId", staticFlags);
		if (focusedProperty != null) { focusedId = System.Convert.ToUInt64(focusedProperty.GetValue(null)); }
	}
	var dragContainer = target.GetComponent<Container>();
	var dragChildren = new System.Collections.Generic.List<Item>();
	if (dragContainer != null) {
		for (var i = 0; i < dragContainer.transform.childCount; i++) {
			var childItem = dragContainer.transform.GetChild(i).GetComponent<Item>();
			if (childItem != null) { dragChildren.Add(childItem); }
		}
	}
	var probeChild = dragChildren.Count > 0 ? dragChildren[0] : null;
	var castContentsBefore = castContainer == null ? -1 : castContainer.transform.childCount;
	var pairCulture = System.Globalization.CultureInfo.InvariantCulture;
	var childJson = new System.Collections.Generic.List<string>();
	for (var i = 0; i < dragChildren.Count; i++) {
		var childItem = dragChildren[i];
		var childMarker = markerOf(markerType, childItem);
		childJson.Add("{\"type\":\"" + childItem.id + "\""
			+ ",\"id\":" + (childMarker == null ? "0" : fieldOf(childMarker, "Id").ToString(pairCulture))
			+ ",\"proxy\":" + (isProxy(proxyType, childItem) ? "true" : "false")
			+ ",\"x\":" + childItem.transform.position.x.ToString("0.###", pairCulture)
			+ ",\"y\":" + childItem.transform.position.y.ToString("0.###", pairCulture)
			+ ",\"toCast\":" + (castButtonItem == null ? "-1" : UnityEngine.Vector2.Distance(childItem.transform.position, castButtonItem.transform.position).ToString("0.###", pairCulture))
			+ ",\"canHold\":" + (castContainer == null ? "false" : (castContainer.CanHoldItem(childItem) ? "true" : "false"))
			+ "}");
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
		+ ",\"viewOpen\":" + (viewOpen ? "true" : "false")
		+ ",\"focusedSteamId\":" + focusedId.ToString(pairCulture)
		+ ",\"focusSet\":" + (focusSet ? "true" : "false")
		+ ",\"castIsBody\":" + (castButton != null && castButton.isBody ? "true" : "false")
		+ ",\"castOverlaps\":" + (castOverlaps ? "true" : "false")
		+ ",\"castItem\":\"" + (castButtonItem == null ? "none" : castButtonItem.id) + "\""
		+ ",\"castItemId\":" + (castMarker == null ? "0" : fieldOf(castMarker, "Id").ToString(pairCulture))
		+ ",\"castItemProxy\":" + (castButtonItem != null && isProxy(proxyType, castButtonItem) ? "true" : "false")
		+ ",\"castItemOwner\":" + (castMarker == null ? "0" : fieldOf(castMarker, "OwnerSteamId").ToString(pairCulture))
		+ ",\"castItemParent\":\"" + (castButtonItem == null || castButtonItem.transform.parent == null ? "none" : castButtonItem.transform.parent.name) + "\""
		+ ",\"castIsContainer\":" + (castContainer != null ? "true" : "false")
		+ ",\"castContentsBefore\":" + castContentsBefore.ToString(pairCulture)
		+ ",\"castContentsAfter\":" + (castContainer == null ? "-1" : castContainer.transform.childCount.ToString(pairCulture))
		+ ",\"castPosX\":" + (castButtonItem == null ? "-1" : castButtonItem.transform.position.x.ToString("0.###", pairCulture))
		+ ",\"castPosY\":" + (castButtonItem == null ? "-1" : castButtonItem.transform.position.y.ToString("0.###", pairCulture))
		+ ",\"dragIsContainer\":" + (dragContainer != null ? "true" : "false")
		+ ",\"dragPosX\":" + target.transform.position.x.ToString("0.###", pairCulture)
		+ ",\"dragPosY\":" + target.transform.position.y.ToString("0.###", pairCulture)
		+ ",\"childCount\":" + dragChildren.Count.ToString(pairCulture)
		+ ",\"children\":[" + string.Join(",", childJson) + "]"
		+ ",\"childInCast\":" + (probeChild != null && castContainer != null && probeChild.transform.parent == castContainer.transform ? "true" : "false")
		+ ",\"childParentAfter\":\"" + (probeChild == null || probeChild.transform.parent == null ? "none" : probeChild.transform.parent.name) + "\""
		+ ",\"calls\":\"" + failure + "\"}";
}))()
