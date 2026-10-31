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
	return "{\"ok\":true,\"opened\":true}";
}))()
