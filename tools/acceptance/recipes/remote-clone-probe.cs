// recipe: remote-clone-probe
// args: mode=s
// returns: ok, mode, cloneCount, playerCount, cloneType, cloneCharType, componentCount, components, facing, error, detail
//
// Names the real type behind a remote player's clone and the facing data it carries — the reading the pose/head-orientation ticket
// stopped at: a `Body` scan found no remote body on either client, so the host's view of the teammate is rendered by something else
// and the probe of that turn was removed rather than committed as dead tooling. `mode=components` lists the components on the first
// clone (their type names), so the answer names the surface instead of guessing it; `mode=facing` reports the fields that look like a
// facing or look target when the clone carries them.
//
// Read-only. It walks the components of every `RemoteCloneRender` in the scene — CUO's own marker on a remote clone — and never writes.
((System.Func<string>)(() => {
	var mode = {{s:mode}};
	System.Type cloneType = null;
	var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
	for (var i = 0; i < assemblies.Length; i++) {
		try { cloneType = assemblies[i].GetType("CasualtiesUnknownOnline.GameAdapter.Character.RemoteCloneRender", false); } catch { cloneType = null; }
		if (cloneType != null) { break; }
	}
	if (cloneType == null) { return "{\"ok\":false,\"error\":\"no-clone-type\"}"; }

	var found = UnityEngine.Object.FindObjectsOfType(cloneType);
	if (found == null || found.Length == 0) {
		// The player clone carries `RemoteBodyDriver`, not the item marker: the inventory renderer marks item proxies,
		// while `RemoteBodyFactory` attaches the driver to the body it builds. Fall back to the driver so the row reads
		// the clone the product actually creates (measured 2026-11-22: the host logged "Remote body … built from the
		// viewer's own body" on the same run this probe answered cloneCount 0).
		System.Type driverType = null;
		for (var i = 0; i < assemblies.Length; i++) {
			try { driverType = assemblies[i].GetType("CasualtiesUnknownOnline.GameAdapter.Character.RemoteBodyDriver", false); } catch { driverType = null; }
			if (driverType != null) { break; }
		}
		if (driverType != null) {
			var drivers = UnityEngine.Object.FindObjectsOfType(driverType);
			if (drivers != null && drivers.Length > 0) {
				found = new UnityEngine.Object[drivers.Length];
				for (var i = 0; i < drivers.Length; i++) { found[i] = drivers[i]; }
			}
		}
	}
	if (found == null || found.Length == 0) { return "{\"ok\":true,\"cloneCount\":0}"; }
	// The marker also sits on carried-item clones. The player's clone is the one whose hierarchy
	// carries the game's own Body; selecting the first marker read an item and reported charType -1.
	UnityEngine.Component clone = null;
	Body cloneBody = null;
	for (var i = 0; i < found.Length; i++) {
		var candidate = found[i] as UnityEngine.Component;
		if (candidate == null) { continue; }
		var body = candidate.GetComponentInChildren<Body>();
		if (body != null) { clone = candidate; cloneBody = body; break; }
	}
	if (clone == null) {
		// The player clone carries `RemoteBodyDriver`, not the item marker: the inventory renderer marks item proxies,
		// while `RemoteBodyFactory` attaches the driver to the body it builds. Fall back to the driver so the row reads
		// the clone the product actually creates (measured 2026-11-22: the host logged "Remote body … built from the
		// viewer's own body" on the same run this probe answered cloneCount 0).
		System.Type driverType = null;
		for (var i = 0; i < assemblies.Length; i++) {
			try { driverType = assemblies[i].GetType("CasualtiesUnknownOnline.GameAdapter.Character.RemoteBodyDriver", false); } catch { driverType = null; }
			if (driverType != null) { break; }
		}
		if (driverType != null) {
			var drivers = UnityEngine.Object.FindObjectsOfType(driverType);
			if (drivers != null && drivers.Length > 0) {
				found = new UnityEngine.Object[drivers.Length];
				for (var i = 0; i < drivers.Length; i++) { found[i] = drivers[i]; }
				for (var i = 0; i < found.Length; i++) {
					var candidate = found[i] as UnityEngine.Component;
					if (candidate == null) { continue; }
					var driverBody = candidate.GetComponent<Body>();
					if (driverBody != null) { clone = candidate; cloneBody = driverBody; break; }
				}
			}
		}
	}
	if (clone == null) { clone = found[0] as UnityEngine.Component; }
	if (clone == null) { return "{\"ok\":false,\"error\":\"no-clone-body\",\"cloneCount\":" + found.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}"; }

	var components = clone.GetComponents<UnityEngine.Component>();
	var names = "";
	var facing = "";
	var cloneCharType = cloneBody != null ? cloneBody.charType : -1;
	var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	for (var i = 0; i < components.Length; i++) {
		var component = components[i];
		if (component == null) { continue; }
		var type = component.GetType();
		if (names.Length > 0) { names += ","; }
		names += type.Name;
		if (cloneCharType < 0 && type.Name == "Body") {
			var charField = type.GetField("charType", flags);
			if (charField != null) {
				try {
					var raw = charField.GetValue(component);
					if (raw is int) { cloneCharType = (int)raw; }
				}
				catch { cloneCharType = -1; }
			}
		}

		if (facing.Length > 0) { continue; }
		var fields = type.GetFields(flags);
		for (var f = 0; f < fields.Length; f++) {
			var name = fields[f].Name;
			var looks = name == "isRight" || name == "targetLookPos" || name == "facing" || name == "lookPos" || name == "headRotation";
			if (!looks) { continue; }
			object value = null;
			try { value = fields[f].GetValue(component); } catch { value = null; }
			if (facing.Length > 0) { facing += ";"; }
			facing += type.Name + "." + name + "=" + (value == null ? "null" : value.ToString());
		}
	}

	var playerCount = 0;
	for (var c = 0; c < found.Length; c++) {
		var candidate = found[c] as UnityEngine.Component;
		if (candidate == null) { continue; }
		var parts = candidate.GetComponents<UnityEngine.Component>();
		for (var p = 0; p < parts.Length; p++) {
			if (parts[p] != null && parts[p].GetType().Name == "Body") { playerCount++; break; }
		}
	}

	return "{\"ok\":true,\"mode\":\"" + mode + "\",\"cloneCount\":" + found.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"playerCount\":" + playerCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"cloneType\":\"" + cloneType.Name + "\",\"cloneCharType\":" + cloneCharType.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"componentCount\":" + components.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"components\":\"" + names + "\",\"facing\":\"" + facing + "\"}";
}))()
