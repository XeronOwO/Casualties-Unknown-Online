// recipe: remote-clone-probe
// args: mode=s
// returns: ok, mode, cloneCount, cloneType, componentCount, components, facing, error, detail
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
	if (found == null || found.Length == 0) { return "{\"ok\":true,\"cloneCount\":0}"; }
	var clone = found[0] as UnityEngine.Component;
	if (clone == null) { return "{\"ok\":false,\"error\":\"clone-not-component\"}"; }

	var components = clone.GetComponents<UnityEngine.Component>();
	var names = "";
	var facing = "";
	var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
	for (var i = 0; i < components.Length; i++) {
		var component = components[i];
		if (component == null) { continue; }
		var type = component.GetType();
		if (names.Length > 0) { names += ","; }
		names += type.Name;

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

	return "{\"ok\":true,\"mode\":\"" + mode + "\",\"cloneCount\":" + found.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"cloneType\":\"" + cloneType.Name + "\",\"componentCount\":" + components.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)
		+ ",\"components\":\"" + names + "\",\"facing\":\"" + facing + "\"}";
}))()
