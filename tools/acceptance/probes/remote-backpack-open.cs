// recipe: remote-backpack-open
// args: none
// returns: ok, opened, error, detail
//
// Opens the teammate's DISPLAYED BACKPACK for the drag currently held, through the adapter's own patch-bridge entry — the same call
// the drag path runs when the player drags an item toward a teammate (`IRemoteBackpackPatchBridge.AutoOpenRemoteBackpackForDrag`).
// It exists because the drag-use cue's target is resolved against that displayed backpack: with a real drag held and the pointer on
// the teammate, `drag-cue` answered showing=false until the view was open (measured 2026-11-01).
//
// The bridge is reachable (`PatchBridge.Impl`) and the method is part of its interface, so this needs no service graph: it hands the
// game's own `PlayerCamera.dragItem` to the call the drag path already makes.
((System.Func<string>)(() => {
	var camera = PlayerCamera.main;
	if (camera == null) { return "{\"ok\":false,\"error\":\"no-camera\"}"; }
	var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var dragField = typeof(PlayerCamera).GetField("dragItem", flags);
	var item = dragField == null ? null : dragField.GetValue(camera);
	if (item == null) { return "{\"ok\":false,\"error\":\"no-drag\",\"detail\":\"hold a drag first\"}"; }

	System.Type bridgeType = null;
	var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
	for (var i = 0; i < assemblies.Length; i++) {
		try { bridgeType = assemblies[i].GetType("CasualtiesUnknownOnline.GameAdapter.PatchBridge", false); } catch { bridgeType = null; }
		if (bridgeType != null) { break; }
	}
	if (bridgeType == null) { return "{\"ok\":false,\"error\":\"no-bridge\"}"; }
	var impl = bridgeType.GetProperty("Impl", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic) == null ? null : bridgeType.GetProperty("Impl", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic).GetValue(null);
	if (impl == null) { return "{\"ok\":false,\"error\":\"no-impl\"}"; }

	System.Reflection.MethodInfo open = null;
	var methods = impl.GetType().GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
	for (var i = 0; i < methods.Length; i++) {
		if (methods[i].Name == "AutoOpenRemoteBackpackForDrag") { open = methods[i]; break; }
	}
	if (open == null) { return "{\"ok\":false,\"error\":\"no-auto-open\"}"; }

	open.Invoke(impl, new object[] { item });

	// The verdict is the VIEW'S OWN state, never the fact that the call returned: the auto-open refuses silently for a target with no
	// render clone, out of range, or with no target at all, and a recipe that answered "opened" for the call's return would hide every
	// one of those (measured 2026-11-01: opened=true reported while the frame showed no teammate panel).
	System.Type view = null;
	for (var i = 0; i < assemblies.Length; i++) {
		try { view = assemblies[i].GetType("CasualtiesUnknownOnline.GameAdapter.RemoteBackpackView", false); } catch { view = null; }
		if (view != null) { break; }
	}
	if (view == null) { return "{\"ok\":false,\"error\":\"no-view\"}"; }
	var statics = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	var isOpenProperty = view.GetProperty("IsOpen", statics);
	var focusedProperty = view.GetProperty("FocusedSteamId", statics);
	var isOpen = isOpenProperty == null ? false : (bool)isOpenProperty.GetValue(null, null);
	var focused = focusedProperty == null ? 0UL : (ulong)focusedProperty.GetValue(null, null);
	return "{\"ok\":true,\"isOpen\":" + (isOpen ? "true" : "false") + ",\"focused\":" + focused.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}";
}))()
