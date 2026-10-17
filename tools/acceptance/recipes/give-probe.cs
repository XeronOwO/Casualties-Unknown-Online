// recipe: give-probe
// args: mode=s target=s slot=n
//        mode is `probe` (report what the reflection can reach) or `give` (drive one give intent).
//        Each token on the args line must be <key>=s or <key>=n.
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

	var bridgeType = findType("PatchBridge");
	if (bridgeType == null) { return "{\"ok\":false,\"error\":\"no-PatchBridge-type\"}"; }

	var implProperty = bridgeType.GetProperty("Impl", anyStatic);
	if (implProperty == null) { return "{\"ok\":false,\"error\":\"no-Impl\"}"; }
	var impl = implProperty.GetValue(null);
	if (impl == null) { return "{\"ok\":false,\"error\":\"bridge-not-bound\"}"; }
	trace.Add("bridge:" + impl.GetType().Name);

	// The session, reached through the bridge's own private domains handle — the same object the dispatcher reads
	// its local id from, so what this reports is the identity the intent will really carry.
	ulong localSteamId = 0;
	var domainsField = impl.GetType().GetField("domains", anyInstance) ?? impl.GetType().GetField("Domains", anyInstance);
	if (domainsField != null) {
		var domains = domainsField.GetValue(impl);
		var sessionField = domains == null ? null : domains.GetType().GetField("Session", anyInstance);
		var session = sessionField == null ? null : sessionField.GetValue(domains);
		if (session != null) {
			var local = session.GetType().GetProperty("LocalSteamId", anyInstance);
			if (local != null) { localSteamId = System.Convert.ToUInt64(local.GetValue(session)); }
			trace.Add("session");
		}
	}

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
		if (candidate == null || candidate.GetComponentInParent<Container>() != null) { continue; }
		held = candidate;
		break;
	}
	if (held == null) { return "{\"ok\":false,\"error\":\"no-item\",\"detail\":\"the local body carries no item outside a container\"}"; }

	var report = impl.GetType().GetMethod("ReportLocalReleaseOntoProxy", anyInstance);
	if (report == null) { return "{\"ok\":false,\"error\":\"no-ReportLocalReleaseOntoProxy\"}"; }

	try {
		report.Invoke(impl, new object[] { held, target, "recipe", 0UL, slotText });
		return "{\"ok\":true,\"mode\":\"give\",\"item\":\"" + held.id + "\",\"target\":" + target
			+ ",\"slot\":" + slotText + ",\"localSteamId\":" + localSteamId + "}";
	} catch (System.Exception error) {
		var inner = error.InnerException ?? error;
		return "{\"ok\":false,\"error\":\"invoke-failed\",\"detail\":\"" + inner.GetType().Name + ": " + inner.Message.Replace("\"", "'") + "\"}";
	}
}))()
