// recipe: remote-medical-release
// args: item=s limb=n
// serves: mod-item-authored-actions, mod-authored-effects, mod-cross-player-native-semantics
// returns: ok, item, slot, limb, limbLookingAt, viewWasOpen, viewOpen, dragAfter, calls, error, detail
//
// The REMOTE half of the wound-view release: with CUO's remote medical view open on a peer, it releases
// a LOCAL carried item onto that peer's displayed limb through the game's own release path
// (`PlayerCamera.HandleReleaseDragging` -> `TryPerformSpecialUIAction`), which CUO's remote-medical
// patch routes to the host-authoritative limb-use chain. The committed `remote-gesture mode=medical`
// covers the client's OWN limb and refuses this case by design, so a cross-player limb-tool / topical /
// injection row had no committed way to make the gesture until this recipe.
//
// What the run owes before it: the remote medical view open on the target
// (`remote-wound-special mode=open target=<steamid>`), the game's own wound view open and pointed at
// the limb (`remote-gesture mode=wound-view limb=<n>`), and the item carried by the acting client
// (`item-provide mode=create`). The release's own outcome is asynchronous — the authority answers after
// asking the treated player's client — so the verdict is read from the operator's and the treated
// client's own logs and limb state, never from this return value.
((System.Func<string>)(() => {
	const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var inv = System.Globalization.CultureInfo.InvariantCulture;
	var limbIndex = (int){{n:limb}};
	var itemId = {{s:item}};
	var camera = PlayerCamera.main;
	if (camera == null) { return "{\"ok\":false,\"error\":\"no-camera\"}"; }
	var body = camera.body;
	if (body == null) { return "{\"ok\":false,\"error\":\"no-local-body\"}"; }
	var carried = (Item)null;
	var carriedSlot = -1;
	for (var i = 0; i < body.slots.Length; i++) {
		var candidate = body.GetItem(i);
		if (candidate != null && candidate.id == itemId) { carried = candidate; carriedSlot = i; break; }
	}
	if (carried == null) { return "{\"ok\":false,\"error\":\"not-carried\",\"detail\":\"no slot holds " + itemId + "\"}"; }
	var viewObject = camera.woundView;
	if (viewObject == null) { return "{\"ok\":false,\"error\":\"no-wound-view\"}"; } // Unity object — ==
	var view = viewObject.GetComponent<WoundView>();
	if (view == null) { return "{\"ok\":false,\"error\":\"no-wound-view-component\"}"; }
	var wasOpen = viewObject.activeSelf;
	if (!wasOpen) { camera.ToggleWoundView(true); }
	// The displayed limb the view points at, through the game's own hit entry (WoundViewLimb.OnPointerEnter).
	var limbObjects = UnityEngine.Object.FindObjectsOfType(typeof(WoundViewLimb));
	WoundViewLimb hit = null;
	for (var i = 0; i < limbObjects.Length; i++) {
		var candidate = limbObjects[i] as WoundViewLimb;
		if (candidate != null && candidate.woundview == view && candidate.limb == limbIndex) { hit = candidate; break; } // Unity object — ==
	}
	if (hit == null) { return "{\"ok\":false,\"error\":\"no-wound-view-limb\",\"detail\":\"limb " + limbIndex.ToString(inv) + "\"}"; }
	hit.OnPointerEnter(null);
	var lookingAt = view.limbLookingAt;
	// Stage the drag and invoke the game's own release with the limb object as the frame's only cast.
	camera.dragItem = carried;
	var casts = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
	var raycast = new UnityEngine.EventSystems.RaycastResult();
	raycast.gameObject = hit.gameObject;
	casts.Add(raycast);
	var release = typeof(PlayerCamera).GetMethod("HandleReleaseDragging", flags);
	if (release == null) { return "{\"ok\":false,\"error\":\"no-release-method\"}"; }
	var failure = "none";
	try { release.Invoke(camera, new object[] { casts }); } catch (System.Exception ex) { failure = ex.GetType().Name + ": " + ex.Message; }
	var dragAfter = camera.dragItem != null ? camera.dragItem.id : "none"; // Unity object — ==
	return "{\"ok\":true,\"item\":\"" + itemId + "\",\"slot\":" + carriedSlot.ToString(inv)
		+ ",\"limb\":" + limbIndex.ToString(inv) + ",\"limbLookingAt\":" + lookingAt.ToString(inv)
		+ ",\"viewWasOpen\":" + (wasOpen ? "true" : "false")
		+ ",\"viewOpen\":" + (viewObject.activeSelf ? "true" : "false")
		+ ",\"dragAfter\":\"" + dragAfter + "\",\"calls\":\"" + failure + "\"}";
}))()
