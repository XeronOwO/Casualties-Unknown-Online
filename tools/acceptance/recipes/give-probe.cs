// recipe: give-probe
// args: mode=s target=s slot=n container=n
//        mode is `probe` (report what the reflection can reach) or `give` (drive one give intent).
//        slot is the destination ring slot, -1 when the destination is a container; container is that
//        container's instance id, 0 when the destination is a slot. Each token on the args line must be
//        <key>=s or <key>=n.
// serves: todo/give-item-to-another-player.md - its six acceptance rows all need "release my own item onto
//         another player's displayed backpack", which the native UI decides from the cursor. The release seam
//         itself is a plain method on CUO's patch bridge (`ReportLocalReleaseOntoProxy`), so a run can call it
//         directly from inside the process: no cursor, no OS input, and exactly the call the drag patch makes.
// returns: probe -> ok, bridge, localSteamId, target, trace; give -> ok, item, target, slot, or the refusal.
//
// Why reflection rather than a reference: the bridge and its interfaces are internal to the adapter, and an
// in-process eval cannot compile against them. Everything below is looked up BY NAME for that reason, and the
// item is handed to that method as a real `Item` - the callee derives the id itself, which is why this recipe
// never needs to.
((System.Func<string>)(() => {
	var mode = {{s:mode}};
	var targetText = {{s:target}};
	var slotText = {{n:slot}};
	var containerText = {{n:container}};
	var trace = new System.Collections.Generic.List<string>();

	var findType = new System.Func<string, System.Type>((simpleName) => {
		var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
		for (var i = 0; i < assemblies.Length; i++) {
			System.Type[] types;
			try { types = assemblies[i].GetTypes(); } catch { continue; }
			for (var t = 0; t < types.Length; t++) {
				if (types[t].Name == simpleName) { return types[t]; }
			}
		}
		return null;
	});

	const System.Reflection.BindingFlags anyStatic = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
	const System.Reflection.BindingFlags anyInstance = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

	// The item's CUO instance id, read off its own component by NAME — the type is internal to the adapter, and
	// this recipe only needs the number the release seam would refuse an id-less item for.
	var itemIdOf = new System.Func<Item, ulong>((candidate) => {
		var parts = candidate.GetComponents<UnityEngine.Component>();
		for (var p = 0; p < parts.Length; p++) {
			if (parts[p] == null || parts[p].GetType().Name != "ItemInstanceId") { continue; }
			var idProperty = parts[p].GetType().GetProperty("Id");
			if (idProperty != null) { return System.Convert.ToUInt64(idProperty.GetValue(parts[p])); }
		}
		return 0UL;
	});

	var bridgeType = findType("PatchBridge");
	if (bridgeType == null) { return "{\"ok\":false,\"error\":\"no-PatchBridge-type\"}"; }

	var implProperty = bridgeType.GetProperty("Impl", anyStatic);
	if (implProperty == null) { return "{\"ok\":false,\"error\":\"no-Impl\"}"; }
	var impl = implProperty.GetValue(null);
	if (impl == null) { return "{\"ok\":false,\"error\":\"bridge-not-bound\"}"; }
	trace.Add("bridge:" + impl.GetType().Name);

	// The bridge exposes the identity itself (LocalSteamId), which is the same value the dispatcher reads.
	// Reaching it through the primary constructor's captured parameter does not work: that parameter is not a field.
	ulong localSteamId = 0;
	var localProperty = impl.GetType().GetProperty("LocalSteamId", anyInstance);
	if (localProperty != null) { localSteamId = System.Convert.ToUInt64(localProperty.GetValue(impl)); trace.Add("localSteamId"); }

	ulong target = 0;
	if (!string.IsNullOrEmpty(targetText) && targetText != "none") { target = System.Convert.ToUInt64(targetText); }

	if (mode == "probe") {
		return "{\"ok\":true,\"mode\":\"probe\",\"bridge\":\"" + impl.GetType().Name + "\",\"localSteamId\":" + localSteamId
			+ ",\"target\":" + target + ",\"trace\":\"" + string.Join(" | ", trace.ToArray()) + "\"}";
	}

	if (mode != "give") { return "{\"ok\":false,\"error\":\"bad-mode\",\"detail\":\"mode must be probe or give\"}"; }

	// The giver's own item, picked from the local body's children rather than from a slot list, and skipping
	// anything inside a container so the pick is an item the body itself carries.
	var camera = PlayerCamera.main;
	var body = camera == null ? null : camera.body;
	if (body == null) { return "{\"ok\":false,\"error\":\"no-body\"}"; }

	Item held = null;
	var candidates = body.GetComponentsInChildren<Item>(true);
	for (var i = 0; i < candidates.Length; i++) {
		var candidate = candidates[i];
		// A SLOT item: sitting in an inventory slot and not inside a container. The host path refuses a worn item
		// by name ("only backpack/hand slot items are takeable in this slice"), and the slot itself hangs under a
		// limb — so a "no limb ancestor" test would skip every slot item, which is what the first attempt did.
		if (candidate == null) { continue; }
		if (candidate.GetComponentInParent<Container>() != null) { continue; }
		if (candidate.GetComponentInParent<InventorySlot>() == null) { continue; }
		// An item with NO instance id is refused by the release seam itself ("it cannot be named to that player"),
		// which would make every later reading describe that refusal instead of the one under test. A starting
		// supply item can be id-less; an item the game handed out through a pickup is not.
		if (itemIdOf(candidate) == 0UL) { continue; }
		held = candidate;
		break;
	}
	if (held == null) { return "{\"ok\":false,\"error\":\"no-item\",\"detail\":\"the local body carries no identified item outside a container\"}"; }

	var report = impl.GetType().GetMethod("ReportLocalReleaseOntoProxy", anyInstance);
	if (report == null) { return "{\"ok\":false,\"error\":\"no-ReportLocalReleaseOntoProxy\"}"; }

	try {
		report.Invoke(impl, new object[] { held, target, "recipe", (ulong)containerText, slotText });
		return "{\"ok\":true,\"mode\":\"give\",\"item\":\"" + held.id + "\",\"target\":" + target
			+ ",\"slot\":" + slotText + ",\"container\":" + containerText + ",\"localSteamId\":" + localSteamId + "}";
	} catch (System.Exception error) {
		var inner = error.InnerException ?? error;
		return "{\"ok\":false,\"error\":\"invoke-failed\",\"detail\":\"" + inner.GetType().Name + ": " + inner.Message.Replace("\"", "'") + "\"}";
	}
}))()
